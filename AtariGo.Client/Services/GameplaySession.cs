using System.Diagnostics;
using System.IO;

using Grpc.Core;

using AtariGo.Client.Models;
using AtariGo.Contracts;

namespace AtariGo.Client.Services;

public sealed class GameplaySession : IGameplaySession
{
    private const long UninitializedVersion = -1;
    private const int UnsentAttempt = -1;

    private static readonly TimeSpan reconnectDelay = TimeSpan.FromSeconds(1);

    private readonly IGameSessionClient _client;
    private readonly object _sync = new object();
    private readonly CancellationTokenSource _lifetimeCancellation = new CancellationTokenSource();
    private readonly CancellationToken _lifetimeToken;
    private IGameSessionStream? _stream;
    private CancellationTokenSource? _attempt;
    private GameSessionClientEvent? _pendingAction;
    private Task? _runTask;
    private long _gameId;
    private long _version = UninitializedVersion;
    private int _attemptNumber;
    private int _sentAttempt = UnsentAttempt;
    private bool _isDisposed;
    private bool _isFinished;
    private GameplayConnectionState _state = GameplayConnectionState.Connecting;
    private ClientFailureReason _failure;

    public GameplaySession(IGameSessionClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
        _lifetimeToken = _lifetimeCancellation.Token;
    }

    public event Action<GameplaySessionUpdate>? Updated;

    public Task RunAsync(long gameId, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(gameId);
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            if (_runTask is not null)
            {
                if (_gameId != gameId)
                {
                    throw new InvalidOperationException("A gameplay session belongs to one game.");
                }
                return _runTask;
            }
            _gameId = gameId;
            _runTask = RunCoreAsync(cancellationToken);
            return _runTask;
        }
    }

    public async Task SubmitMoveAsync(int row, int column, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(row);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(row, GameRules.BoardSize);
        ArgumentOutOfRangeException.ThrowIfNegative(column);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(column, GameRules.BoardSize);
        using CancellationTokenSource operation = CreateCancellation(cancellationToken);
        operation.Token.ThrowIfCancellationRequested();
        bool queued;
        lock (_sync)
        {
            queued = SetPendingAction(new GameSessionClientEvent
            {
                SubmitMove = new SubmitGameMove
                {
                    ActionId = Guid.NewGuid().ToString("N"),
                    ExpectedVersion = _version,
                    Row = row,
                    Column = column
                }
            });
        }
        if (queued)
        {
            PublishCurrent();
            await SendPendingAsync(operation.Token).ConfigureAwait(false);
        }
    }

    public async Task SurrenderAsync(CancellationToken cancellationToken)
    {
        using CancellationTokenSource operation = CreateCancellation(cancellationToken);
        operation.Token.ThrowIfCancellationRequested();
        bool queued;
        lock (_sync)
        {
            queued = SetPendingAction(new GameSessionClientEvent
            {
                Surrender = new SurrenderGame { ActionId = Guid.NewGuid().ToString("N") }
            });
        }
        if (queued)
        {
            PublishCurrent();
            await SendPendingAsync(operation.Token).ConfigureAwait(false);
        }
    }

    public async Task<bool> SendChatAsync(string text, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        using CancellationTokenSource operation = CreateCancellation(cancellationToken);
        IGameSessionStream? stream;
        CancellationTokenSource? attempt;
        lock (_sync)
        {
            stream = _state == GameplayConnectionState.Connected ? _stream : null;
            attempt = _attempt;
        }
        if (stream is null || attempt is null)
        {
            return false;
        }
        bool sent = false;
        try
        {
            await stream.SendChatMessageAsync(text, operation.Token).ConfigureAwait(false);
            operation.Token.ThrowIfCancellationRequested();
            sent = true;
        }
        catch (Exception exception) when (IsTransportFailure(exception))
        {
            HandleFailure(attempt, exception, true);
        }
        return sent;
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
            _pendingAction = null;
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

    private async Task RunCoreAsync(CancellationToken cancellationToken)
    {
        await Task.Yield();
        try
        {
            using CancellationTokenSource lifetime = CreateCancellation(cancellationToken);
            CancellationToken token = lifetime.Token;
            bool retry = true;
            while (retry && !token.IsCancellationRequested)
            {
                retry = await TryReadAsync(token).ConfigureAwait(false);
                if (retry)
                {
                    await Task.Delay(reconnectDelay, token).ConfigureAwait(false);
                }
            }
        }
        catch (Exception exception) when (_lifetimeToken.IsCancellationRequested
            || GrpcCallPolicy.IsRequestedCancellation(exception, cancellationToken))
        {
            Trace.TraceInformation("Gameplay session stopped with its owner.");
        }
        catch (Exception exception)
        {
            Trace.TraceWarning("Gameplay session failed: {0}.", exception.GetType().Name);
            lock (_sync)
            {
                _state = GameplayConnectionState.Closed;
                _failure = GrpcClientFailures.GetReason(exception);
            }
            PublishCurrent();
        }
    }

    private async Task<bool> TryReadAsync(CancellationToken cancellationToken)
    {
        using CancellationTokenSource attempt =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lock (_sync)
        {
            _attempt = attempt;
            _attemptNumber++;
            _state = GameplayConnectionState.Connecting;
            _failure = ClientFailureReason.None;
        }
        PublishCurrent(connectionStarted: true);
        try
        {
            await ReadEventsAsync(attempt.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (attempt.IsCancellationRequested)
        {
            Trace.TraceInformation("Gameplay connection attempt stopped.");
        }
        catch (Exception exception) when (IsTransportFailure(exception))
        {
            HandleFailure(attempt, exception, false);
        }
        finally
        {
            lock (_sync)
            {
                _stream = null;
                _attempt = null;
                if (_state != GameplayConnectionState.Closed)
                {
                    _state = GameplayConnectionState.Recovering;
                }
            }
            PublishCurrent();
        }
        lock (_sync)
        {
            return !_isDisposed && !cancellationToken.IsCancellationRequested
                && _state != GameplayConnectionState.Closed;
        }
    }

    private async Task ReadEventsAsync(CancellationToken cancellationToken)
    {
        await using IGameSessionStream stream = await _client.ConnectAsync(_gameId,
            cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            _stream = stream;
        }
        bool restored = false;
        await foreach (GameSessionServerEvent serverEvent in stream.ReadEventsAsync(
            cancellationToken)
            .ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool acceptedSnapshot = ApplyEvent(serverEvent);
            if (!restored && acceptedSnapshot)
            {
                restored = true;
                await SendPendingAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private bool ApplyEvent(GameSessionServerEvent serverEvent)
    {
        bool isSnapshot = serverEvent.EventCase == GameSessionServerEvent.EventOneofCase.Snapshot;
        if (isSnapshot && !GameSessionSnapshotValidator.IsValid(serverEvent.Snapshot, _gameId))
        {
            PublishCurrent(invalidSnapshot: true);
            return false;
        }
        GameplaySessionUpdate update;
        lock (_sync)
        {
            if (_isDisposed || _state == GameplayConnectionState.Closed
                || (isSnapshot && serverEvent.Snapshot.Version < _version))
            {
                return false;
            }
            if (isSnapshot)
            {
                _version = serverEvent.Snapshot.Version;
                _isFinished = serverEvent.Snapshot.Status is "Completed" or "Cancelled";
                _state = GameplayConnectionState.Connected;
                _failure = ClientFailureReason.None;
            }
            bool matching = IsMatchingResult(serverEvent);
            bool surrender = matching && _pendingAction?.Surrender is not null;
            if (matching)
            {
                _pendingAction = null;
            }
            update = CreateUpdate() with
            {
                ServerEvent = serverEvent.Clone(),
                IsMatchingActionResult = matching,
                IsSurrenderResult = surrender
            };
        }
        Updated?.Invoke(update);
        return isSnapshot;
    }

    private bool SetPendingAction(GameSessionClientEvent action)
    {
        if (_isDisposed || _isFinished || _state != GameplayConnectionState.Connected
            || _pendingAction is not null)
        {
            return false;
        }
        _pendingAction = action;
        _sentAttempt = UnsentAttempt;
        return true;
    }

    private bool IsMatchingResult(GameSessionServerEvent serverEvent)
    {
        if (serverEvent.EventCase != GameSessionServerEvent.EventOneofCase.ActionResult)
        {
            return false;
        }
        string? pendingId = _pendingAction?.SubmitMove?.ActionId
            ?? _pendingAction?.Surrender?.ActionId;
        return pendingId is not null && serverEvent.ActionResult.ActionId == pendingId;
    }

    private async Task SendPendingAsync(CancellationToken cancellationToken)
    {
        IGameSessionStream? stream;
        CancellationTokenSource? attempt;
        GameSessionClientEvent action;
        lock (_sync)
        {
            if (_isDisposed || _pendingAction is null || _stream is null || _attempt is null
                || _state != GameplayConnectionState.Connected || _sentAttempt == _attemptNumber)
            {
                return;
            }
            stream = _stream;
            attempt = _attempt;
            action = _pendingAction.Clone();
            _sentAttempt = _attemptNumber;
        }
        try
        {
            await stream.SendEventAsync(action, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsTransportFailure(exception))
        {
            HandleFailure(attempt, exception, false);
        }
    }

    private void HandleFailure(CancellationTokenSource attempt, Exception exception,
        bool chatSendFailed)
    {
        Trace.TraceWarning("Gameplay transport failed: {0}.", exception.GetType().Name);
        lock (_sync)
        {
            if (_isDisposed || !ReferenceEquals(attempt, _attempt))
            {
                return;
            }
            _failure = GrpcClientFailures.GetReason(exception);
            _state = IsPermanentFailure(_failure)
                ? GameplayConnectionState.Closed : GameplayConnectionState.Recovering;
        }
        PublishCurrent(chatSendFailed: chatSendFailed);
        lock (_sync)
        {
            if (ReferenceEquals(attempt, _attempt))
            {
                attempt.Cancel();
            }
        }
    }

    private void PublishCurrent(bool chatSendFailed = false, bool invalidSnapshot = false,
        bool connectionStarted = false)
    {
        GameplaySessionUpdate update;
        lock (_sync)
        {
            if (_isDisposed)
            {
                return;
            }
            update = CreateUpdate() with
            {
                ChatSendFailed = chatSendFailed,
                InvalidSnapshot = invalidSnapshot,
                ConnectionStarted = connectionStarted
            };
        }
        Updated?.Invoke(update);
    }

    private GameplaySessionUpdate CreateUpdate()
    {
        return new GameplaySessionUpdate(_state, _failure, _pendingAction is not null);
    }

    private static bool IsTransportFailure(Exception exception)
    {
        return exception is RpcException or IOException or ObjectDisposedException;
    }

    private static bool IsPermanentFailure(ClientFailureReason failure)
    {
        return failure is ClientFailureReason.SessionExpired or ClientFailureReason.PermissionDenied
            or ClientFailureReason.InvalidRequest or ClientFailureReason.ConnectionReplaced;
    }
}
