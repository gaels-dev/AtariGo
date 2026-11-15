using System.Diagnostics;
using System.IO;

using Grpc.Core;

using AtariGo.Client.Models;
using AtariGo.Contracts;

namespace AtariGo.Client.Services;

public sealed class PrivateRoomSession : IPrivateRoomSession
{
    private static readonly TimeSpan reconnectDelay = TimeSpan.FromSeconds(1);

    private readonly IRoomsClient _client;
    private readonly object _sync = new object();
    private readonly CancellationTokenSource _lifetimeCancellation = new CancellationTokenSource();
    private readonly CancellationToken _lifetimeToken;
    private readonly int _roomId;
    private RoomSnapshot _room;
    private Task? _observationTask;
    private bool _isSynchronized = true;
    private bool _isDisposed;
    private ClientFailureReason _failure;
    private bool _isStartRecovery;

    public PrivateRoomSession(IRoomsClient client, RoomSnapshot room)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(room);
        _client = client;
        _room = room.Clone();
        _roomId = room.RoomId;
        _lifetimeToken = _lifetimeCancellation.Token;
    }

    public event Action<PrivateRoomSessionUpdate>? Updated;

    public RoomSnapshot Room
    {
        get
        {
            lock (_sync)
            {
                return _room.Clone();
            }
        }
    }

    public Task WatchAsync(CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            if (_isDisposed)
            {
                return Task.CompletedTask;
            }
            if (_observationTask is null || _observationTask.IsCompleted)
            {
                _observationTask = ObserveAsync(cancellationToken);
            }
            return _observationTask;
        }
    }

    public async Task StartGameAsync(int captureGoal, CancellationToken cancellationToken)
    {
        if (captureGoal is not (GameRules.DefaultCaptureGoal
            or GameRules.IntermediateCaptureGoal or GameRules.ExtendedCaptureGoal))
        {
            throw new ArgumentOutOfRangeException(nameof(captureGoal));
        }
        using CancellationTokenSource operation = CreateCancellation(cancellationToken);
        CancellationToken token = operation.Token;
        token.ThrowIfCancellationRequested();
        try
        {
            bool confirmed = await ConfirmConfigurationAsync(captureGoal, token)
                .ConfigureAwait(false);
            if (confirmed)
            {
                RoomSnapshot room = Room;
                StartGameRequest request = new StartGameRequest
                {
                    RoomId = _roomId,
                    ExpectedRoomVersion = room.Version
                };
                token.ThrowIfCancellationRequested();
                await _client.StartGameAsync(request, token).ConfigureAwait(false);
            }
        }
        catch (RpcException exception)
            when (!GrpcCallPolicy.IsRequestedCancellation(exception, token))
        {
            await RecoverStartAsync(GrpcClientFailures.GetReason(exception), token)
                .ConfigureAwait(false);
        }
        catch (IOException)
        {
            await RecoverStartAsync(ClientFailureReason.RequestFailed, token).ConfigureAwait(false);
        }
    }

    public async Task<bool> LeaveAsync(CancellationToken cancellationToken)
    {
        using CancellationTokenSource operation = CreateCancellation(cancellationToken);
        CancellationToken token = operation.Token;
        bool left = false;
        try
        {
            await _client.LeaveRoomAsync(_roomId, token).ConfigureAwait(false);
            left = true;
        }
        catch (RpcException exception) when (exception.StatusCode == StatusCode.NotFound)
        {
            left = true;
        }
        catch (RpcException exception)
            when (!GrpcCallPolicy.IsRequestedCancellation(exception, token))
        {
            ReportFailure(GrpcClientFailures.GetReason(exception), false, false);
        }
        catch (IOException)
        {
            ReportFailure(ClientFailureReason.RequestFailed, false, false);
        }
        token.ThrowIfCancellationRequested();
        return left;
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_isDisposed)
            {
                return;
            }
            _isDisposed = true;
        }
        try
        {
            _lifetimeCancellation.Cancel();
        }
        finally
        {
            _lifetimeCancellation.Dispose();
        }
    }

    private CancellationTokenSource CreateCancellation(CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            return CancellationTokenSource.CreateLinkedTokenSource(
                _lifetimeToken, cancellationToken);
        }
    }

    private async Task ObserveAsync(CancellationToken cancellationToken)
    {
        await Task.Yield();
        try
        {
            using CancellationTokenSource observation = CreateCancellation(cancellationToken);
            CancellationToken token = observation.Token;
            bool fullSnapshot = false;
            bool finished = IsFinished(Room);
            PublishCurrent();
            while (!finished)
            {
                token.ThrowIfCancellationRequested();
                finished = await TryObserveAsync(fullSnapshot, token).ConfigureAwait(false);
                if (!finished)
                {
                    ReportDisconnected();
                    await Task.Delay(reconnectDelay, token).ConfigureAwait(false);
                    fullSnapshot = true;
                }
            }
        }
        catch (Exception exception) when (_lifetimeToken.IsCancellationRequested
            || GrpcCallPolicy.IsRequestedCancellation(exception, cancellationToken))
        {
            Trace.TraceInformation("Room observation stopped with its owner.");
        }
        catch (Exception exception)
        {
            Trace.TraceWarning("Room observation failed: {0}.", exception.GetType().Name);
            ReportFailure(GrpcClientFailures.GetReason(exception), false, true);
        }
    }

    private async Task<bool> TryObserveAsync(bool fullSnapshot, CancellationToken token)
    {
        bool finished = false;
        try
        {
            WatchRoomRequest request = new WatchRoomRequest { RoomId = _roomId };
            if (!fullSnapshot)
            {
                request.FromVersion = Room.Version;
            }
            await foreach (RoomSnapshot room in _client.WatchRoomAsync(request, token)
                .ConfigureAwait(false))
            {
                token.ThrowIfCancellationRequested();
                bool accepted = ApplyRoom(room);
                finished = accepted && IsFinished(Room);
                if (finished)
                {
                    break;
                }
            }
        }
        catch (RpcException exception)
            when (!GrpcCallPolicy.IsRequestedCancellation(exception, token))
        {
            ReportFailure(GrpcClientFailures.GetReason(exception), false, true);
            finished = exception.StatusCode is StatusCode.Unauthenticated
                or StatusCode.PermissionDenied or StatusCode.NotFound;
        }
        catch (IOException)
        {
            ReportFailure(ClientFailureReason.RequestFailed, false, true);
        }
        return finished;
    }

    private async Task<bool> ConfirmConfigurationAsync(int captureGoal, CancellationToken token)
    {
        RoomSnapshot room = Room;
        if (captureGoal != room.Configuration.CaptureGoal)
        {
            RoomConfiguration configuration = room.Configuration.Clone();
            configuration.CaptureGoal = captureGoal;
            UpdateRoomConfigurationRequest request = new UpdateRoomConfigurationRequest
            {
                RoomId = _roomId,
                Configuration = configuration,
                ExpectedVersion = room.Version
            };
            RoomSnapshot confirmed = await _client.UpdateRoomConfigurationAsync(request, token)
                .ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            ApplyRoom(confirmed);
        }
        RoomSnapshot current = Room;
        return current.State == RoomState.Waiting
            && current.Configuration.CaptureGoal == captureGoal
            && current.Participants.Any(participant => !participant.IsHost);
    }

    private async Task RecoverStartAsync(ClientFailureReason failure, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        ReportFailure(failure, true, true);
        try
        {
            using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(
                token);
            timeout.CancelAfter(TimeSpan.FromSeconds(GrpcCallPolicy.RequestTimeoutSeconds));
            WatchRoomRequest request = new WatchRoomRequest { RoomId = _roomId };
            await foreach (RoomSnapshot room in _client.WatchRoomAsync(request, timeout.Token)
                .ConfigureAwait(false))
            {
                timeout.Token.ThrowIfCancellationRequested();
                if (ApplyRoom(room))
                {
                    ReportFailure(failure, true, false);
                    break;
                }
            }
        }
        catch (Exception exception) when (!token.IsCancellationRequested)
        {
            Trace.TraceWarning("Room state recovery failed: {0}.", exception.GetType().Name);
            ReportFailure(GrpcClientFailures.GetReason(exception), false, true);
        }
    }

    private bool ApplyRoom(RoomSnapshot room)
    {
        lock (_sync)
        {
            if (_isDisposed || room.RoomId != _roomId || room.Version < _room.Version)
            {
                return false;
            }
            _room = room.Clone();
            if (!_isSynchronized)
            {
                _failure = ClientFailureReason.None;
                _isStartRecovery = false;
            }
            _isSynchronized = true;
        }
        PublishCurrent();
        return true;
    }

    private void ReportDisconnected()
    {
        lock (_sync)
        {
            if (_failure == ClientFailureReason.None)
            {
                _failure = ClientFailureReason.Unavailable;
            }
            _isSynchronized = false;
        }
        PublishCurrent();
    }

    private void ReportFailure(ClientFailureReason failure, bool isStartRecovery,
        bool loseSynchronization)
    {
        lock (_sync)
        {
            _failure = failure;
            _isStartRecovery = isStartRecovery;
            if (loseSynchronization)
            {
                _isSynchronized = false;
            }
        }
        PublishCurrent();
    }

    private void PublishCurrent()
    {
        PrivateRoomSessionUpdate update;
        lock (_sync)
        {
            if (_isDisposed)
            {
                return;
            }
            update = new PrivateRoomSessionUpdate(_room.Clone(), _isSynchronized, _failure)
            {
                IsStartRecovery = _isStartRecovery
            };
        }
        Updated?.Invoke(update);
    }

    private static bool IsFinished(RoomSnapshot room)
    {
        return room.State == RoomState.Closed
            || (room.State == RoomState.Started && room.GameId > 0);
    }
}
