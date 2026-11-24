using System.IO;
using System.Net.NetworkInformation;

using Grpc.Core;

using AtariGo.Client.Models;
using AtariGo.Contracts;
using AtariGo.Diagnostics;

namespace AtariGo.Client.Services;

public sealed class GameplaySession : IGameplaySession
{
    private const long UninitializedVersion = -1;
    private const int UnsentAttempt = -1;
    private const int FailureLogInterval = 10;

    private static readonly TimeSpan reconnectDelay = TimeSpan.FromSeconds(1);

    private readonly IGameSessionClient _client;
    private readonly bool _monitorNetworkAvailability;
    private readonly object _sync = new object();
    private readonly CancellationTokenSource _lifetimeCancellation = new CancellationTokenSource();
    private readonly CancellationToken _lifetimeToken;
    private TaskCompletionSource _retrySignal = new TaskCompletionSource(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private IGameSessionStream? _stream;
    private CancellationTokenSource? _attempt;
    private GameSessionClientEvent? _pendingAction;
    private Task? _runTask;
    private long _gameId;
    private long _version = UninitializedVersion;
    private int _attemptNumber;
    private int _sentAttempt = UnsentAttempt;
    private int _loggedFailureAttempt;
    private Guid _attemptId;
    private bool _isDisposed;
    private bool _isFinished;
    private GameplayConnectionState _state = GameplayConnectionState.Connecting;
    private ClientFailureReason _failure;

    public GameplaySession(IGameSessionClient client, bool monitorNetworkAvailability = false)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
        _monitorNetworkAvailability = monitorNetworkAvailability;
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

    public void RetryConnection()
    {
        CancellationTokenSource? attempt;
        lock (_sync)
        {
            if (_isDisposed || _state is GameplayConnectionState.Connected
                or GameplayConnectionState.Closed)
            {
                return;
            }
            _retrySignal.TrySetResult();
            attempt = _attempt;
        }
        if (attempt is not null)
        {
            try
            {
                attempt.Cancel();
            }
            catch (ObjectDisposedException)
            {
                RecordConnection(GameOperation.ConnectionAttempt, DiagnosticOutcome.Cancelled);
            }
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
        CancellationToken ownerToken = cancellationToken;
        if (_monitorNetworkAvailability)
        {
            NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
        }
        try
        {
            using CancellationTokenSource lifetime = CreateCancellation(cancellationToken);
            CancellationToken token = lifetime.Token;
            ownerToken = token;
            bool retry = true;
            while (retry && !token.IsCancellationRequested)
            {
                retry = await TryReadAsync(token).ConfigureAwait(false);
                if (retry)
                {
                    await WaitForRetryAsync(token).ConfigureAwait(false);
                }
            }
        }
        catch (Exception exception) when (_lifetimeToken.IsCancellationRequested
            || GrpcCallPolicy.IsRequestedCancellation(exception, cancellationToken))
        {
            ClientGameDiagnostics.RecordFailure(
                (GameOperation.StreamFailure, CreateDiagnosticContext()),
                exception, ownerToken);
        }
        catch (Exception exception)
        {
            ClientGameDiagnostics.RecordFailure(
                (GameOperation.StreamFailure, CreateDiagnosticContext()),
                exception, ownerToken);
            lock (_sync)
            {
                _state = GameplayConnectionState.Closed;
                _failure = GrpcClientFailures.GetReason(exception);
            }
            PublishCurrent();
        }
        finally
        {
            if (_monitorNetworkAvailability)
            {
                NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
            }
        }
    }

    private void OnNetworkAvailabilityChanged(object? sender,
        NetworkAvailabilityEventArgs eventArgs)
    {
        if (eventArgs.IsAvailable)
        {
            RetryConnection();
            return;
        }
        CancellationTokenSource? attempt;
        lock (_sync)
        {
            attempt = _state == GameplayConnectionState.Closed ? null : _attempt;
        }
        if (attempt is not null)
        {
            HandleFailure(attempt, new IOException("The local network is unavailable."), false);
        }
    }

    private async Task WaitForRetryAsync(CancellationToken cancellationToken)
    {
        Task signal;
        lock (_sync)
        {
            signal = _retrySignal.Task;
        }
        Task delay = Task.Delay(reconnectDelay, cancellationToken);
        await Task.WhenAny(delay, signal).WaitAsync(cancellationToken).ConfigureAwait(false);
        lock (_sync)
        {
            if (ReferenceEquals(signal, _retrySignal.Task))
            {
                _retrySignal = new TaskCompletionSource(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            }
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
            _attemptId = Guid.NewGuid();
            _state = GameplayConnectionState.Connecting;
            _failure = ClientFailureReason.None;
            if (_attemptNumber is 1 or 2 || _attemptNumber % FailureLogInterval == 0)
            {
                RecordConnection(GameOperation.ConnectionAttempt, DiagnosticOutcome.Started);
            }
        }
        PublishCurrent(connectionStarted: true);
        try
        {
            await ReadEventsAsync(attempt.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception) when (attempt.IsCancellationRequested)
        {
            ClientGameDiagnostics.RecordFailure(
                (GameOperation.ConnectionAttempt, CreateDiagnosticContext()),
                exception, attempt.Token);
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
            if (acceptedSnapshot && serverEvent.Snapshot.TurnConfirmationPending
                && IsPlayersTurn(serverEvent.Snapshot))
            {
                await stream.SendEventAsync(new GameSessionClientEvent
                {
                    AcknowledgeTurn = new AcknowledgeGameTurn
                    {
                        Version = serverEvent.Snapshot.TurnConfirmationVersion
                    }
                }, cancellationToken).ConfigureAwait(false);
            }
            if (!restored && acceptedSnapshot)
            {
                restored = true;
                await SendPendingAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        throw new IOException("The game stream ended before the session was closed.");
    }

    private static bool IsPlayersTurn(GameSessionSnapshot snapshot)
    {
        return snapshot.PlayerStoneColor == "Black"
                && snapshot.CurrentTurn == GameStoneState.Black
            || snapshot.PlayerStoneColor == "White"
                && snapshot.CurrentTurn == GameStoneState.White;
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
            if (ShouldIgnore(serverEvent, isSnapshot))
            {
                return false;
            }
            if (isSnapshot)
            {
                ApplySnapshot(serverEvent.Snapshot);
            }
            bool matching = IsMatchingResult(serverEvent);
            bool surrender = matching && _pendingAction?.Surrender is not null;
            if (matching)
            {
                RecordConfirmation(serverEvent.ActionResult);
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

    private bool ShouldIgnore(GameSessionServerEvent serverEvent, bool isSnapshot)
    {
        return _isDisposed || _state == GameplayConnectionState.Closed
            || (isSnapshot && serverEvent.Snapshot.Version < _version);
    }

    private void ApplySnapshot(GameSessionSnapshot snapshot)
    {
        if (_state != GameplayConnectionState.Connected)
        {
            RecordConnection(_attemptNumber > 1 ? GameOperation.Restored : GameOperation.Connected,
                DiagnosticOutcome.Succeeded);
        }
        _version = snapshot.Version;
        _isFinished = snapshot.Status is "Completed" or "Cancelled";
        _state = GameplayConnectionState.Connected;
        _failure = ClientFailureReason.None;
    }

    private void RecordConfirmation(GameActionResponse response)
    {
        Guid? actionId = Guid.TryParseExact(response.ActionId, "N", out Guid parsed)
            ? parsed : null;
        ClientGameDiagnostics.Record(response.IsAccepted
            ? GameOperation.ActionCommitted : GameOperation.ActionRejected,
            new DiagnosticEventDetails
            {
                Outcome = response.IsAccepted
                    ? DiagnosticOutcome.Succeeded : DiagnosticOutcome.Rejected,
                Context = CreateDiagnosticContext() with { ActionId = actionId },
                Status = GameDiagnostics.CreateReason(response.Rejection)
            });
    }

    private DiagnosticContext CreateDiagnosticContext()
    {
        return new DiagnosticContext { GameId = _gameId, OperationId = _attemptId };
    }

    private void RecordConnection(GameOperation operation, DiagnosticOutcome outcome)
    {
        ClientGameDiagnostics.Record(operation, new DiagnosticEventDetails
        {
            Outcome = outcome,
            Context = CreateDiagnosticContext()
        });
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
        CancellationToken attemptToken;
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
            attemptToken = attempt.Token;
            action = _pendingAction.Clone();
            _sentAttempt = _attemptNumber;
        }
        using CancellationTokenSource sendCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, attemptToken);
        try
        {
            await stream.SendEventAsync(action, sendCancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (attemptToken.IsCancellationRequested
            && !cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (IsTransportFailure(exception))
        {
            HandleFailure(attempt, exception, false);
        }
    }

    private void HandleFailure(CancellationTokenSource attempt, Exception exception,
        bool chatSendFailed)
    {
        bool shouldLog;
        bool isPermanent;
        DiagnosticContext diagnosticContext;
        lock (_sync)
        {
            if (_isDisposed || !ReferenceEquals(attempt, _attempt))
            {
                return;
            }
            _failure = exception is RpcException { StatusCode: StatusCode.FailedPrecondition }
                ? ClientFailureReason.ReconnectionExpired
                : GrpcClientFailures.GetReason(exception);
            isPermanent = IsPermanentFailure(_failure);
            shouldLog = ShouldRecordFailure(isPermanent);
            diagnosticContext = CreateDiagnosticContext();
            _state = isPermanent
                ? GameplayConnectionState.Closed : GameplayConnectionState.Recovering;
        }
        if (shouldLog)
        {
            ClientGameDiagnostics.RecordFailure((chatSendFailed
                ? GameOperation.ChatSubmission : GameOperation.StreamFailure, diagnosticContext),
                exception, attempt.Token);
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

    private bool ShouldRecordFailure(bool isPermanent)
    {
        bool sampled = _attemptNumber == 1 || _attemptNumber % FailureLogInterval == 0;
        bool shouldLog = (sampled || isPermanent) && _loggedFailureAttempt != _attemptNumber;
        if (shouldLog)
        {
            _loggedFailureAttempt = _attemptNumber;
        }
        return shouldLog;
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
            or ClientFailureReason.InvalidRequest or ClientFailureReason.ConnectionReplaced
            or ClientFailureReason.ReconnectionExpired;
    }
}
