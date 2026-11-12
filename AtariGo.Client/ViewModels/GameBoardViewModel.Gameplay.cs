using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;

using Grpc.Core;

using AtariGo.Client.Commands;
using AtariGo.Client.Models;
using AtariGo.Client.Services;
using AtariGo.Contracts;

namespace AtariGo.Client.ViewModels;

public partial class GameBoardViewModel
{
    private const int ClockRefreshMilliseconds = 250;
    private const int ReconnectDelayMilliseconds = 1000;
    private readonly GameClockSynchronization _clockSynchronization =
        new GameClockSynchronization(TimeProvider.System);
    private long _turnDeadlineMilliseconds;
    private readonly DispatcherTimer _clockTimer = new DispatcherTimer();
    private long _gameVersion = -1;
    private bool _isPlaying;
    private bool _isFinished;
    private bool _hasPermanentSessionFailure;
    private bool _returnToMenuAfterSurrender;
    private long _blackClockMilliseconds;
    private long _whiteClockMilliseconds;
    private long _clockReceivedTimestamp;
    private GameSessionClientEvent? _pendingGameAction;
    private CancellationTokenSource? _sessionAttemptCancellation;
    private string _actionStatusMessage = string.Empty;

    public ICommand PlaceStoneCommand { get; private set; } = null!;

    public ICommand SubmitSurrenderCommand { get; private set; } = null!;

    public string ActionStatusMessage
    {
        get => _actionStatusMessage;
        private set => SetProperty(ref _actionStatusMessage, value);
    }

    public bool CanSurrender => !_isDisposed && !_isFinished && !_hasPermanentSessionFailure
        && _gameSessionStream is not null && _pendingGameAction is null;

    public bool CanLeaveGame => !_isDisposed && (!GameId.HasValue || _isFinished
        || _hasPermanentSessionFailure || CanSurrender);

    public string CapturesStatus => string.Format(CultureInfo.CurrentCulture,
        GetLocalizedMessage("GameBoard_Lbl_CapturesStatus"),
        BlackCaptures, WhiteCaptures, TargetCaptures);

    public string ReturnToMenuLabel => GetLocalizedMessage("GameBoard_Button_ReturnToMenu");

    public string BlackTimeLabel => GetLocalizedMessage("GameBoard_Clock_Black");

    public string WhiteTimeLabel => GetLocalizedMessage("GameBoard_Clock_White");

    public string BlackTimeRemaining => FormatClock(_blackClockMilliseconds, StoneColor.Black);

    public string WhiteTimeRemaining => FormatClock(_whiteClockMilliseconds, StoneColor.White);

    private void InitializeGameplay()
    {
        PlaceStoneCommand = new AsyncRelayCommand(PlaceStoneAsync,
            HandleGameplayFailure, _lifetimeCancellation.Token);
        SubmitSurrenderCommand = new AsyncRelayCommand(_ => SurrenderAsync(),
            HandleGameplayFailure, _lifetimeCancellation.Token);
        _clockTimer.Interval = TimeSpan.FromMilliseconds(ClockRefreshMilliseconds);
        _clockTimer.Tick += OnClockTick;
        _clockTimer.Start();
    }

    private async Task PlaceStoneAsync(object? parameter)
    {
        if (parameter is not BoardCellViewModel cell || !IsMyTurn
            || cell.Stone != StoneColor.None)
        {
            return;
        }
        _pendingGameAction = new GameSessionClientEvent
        {
            SubmitMove = new SubmitGameMove
            {
                ActionId = Guid.NewGuid().ToString("N"),
                ExpectedVersion = _gameVersion,
                Row = cell.Row,
                Column = cell.Column
            }
        };
        await SendPendingActionAsync(_lifetimeCancellation.Token);
    }

    private async Task SurrenderAsync()
    {
        if (!CanSurrender || GameId is null)
        {
            return;
        }
        _pendingGameAction = new GameSessionClientEvent
        {
            Surrender = new SurrenderGame { ActionId = Guid.NewGuid().ToString("N") }
        };
        await SendPendingActionAsync(_lifetimeCancellation.Token);
    }

    private async Task SendPendingActionAsync(CancellationToken cancellationToken)
    {
        NotifyGameplayAvailability();
        if (_pendingGameAction is not null && _gameSessionStream is not null)
        {
            ActionStatusMessage = GetLocalizedMessage("GameBoard_Action_Pending");
            await _gameSessionStream.SendEventAsync(_pendingGameAction, cancellationToken);
        }
    }

    private void ApplyActionResult(GameActionResponse result)
    {
        string? pendingId = _pendingGameAction?.SubmitMove?.ActionId
            ?? _pendingGameAction?.Surrender?.ActionId;
        if (result.ActionId != pendingId)
        {
            return;
        }
        bool shouldReturn = _returnToMenuAfterSurrender
            && _pendingGameAction?.Surrender is not null && result.IsAccepted;
        _pendingGameAction = null;
        _returnToMenuAfterSurrender = false;
        ActionStatusMessage = result.IsAccepted ? string.Empty
            : GetLocalizedMessage("GameBoard_Action_" + result.Rejection);
        NotifyGameplayAvailability();
        if (shouldReturn)
        {
            ReturnToMainMenu();
        }
    }

    private void ApplyGameplaySnapshot(GameSessionSnapshot snapshot)
    {
        _gameVersion = snapshot.Version;
        _isPlaying = snapshot.Status == "InProgress";
        _isFinished = snapshot.Status == "Completed" || snapshot.Status == "Cancelled";
        _blackClockMilliseconds = snapshot.BlackRemainingMilliseconds;
        _whiteClockMilliseconds = snapshot.WhiteRemainingMilliseconds;
        _clockReceivedTimestamp = Stopwatch.GetTimestamp();
        _clockSynchronization.ObserveServerTime(snapshot.ServerTimeUnixMilliseconds);
        _turnDeadlineMilliseconds = snapshot.TurnEndsAtUnixMilliseconds;
        BlackCaptures = snapshot.BlackCaptures;
        WhiteCaptures = snapshot.WhiteCaptures;
        SessionStatusMessage = GetGameplayStatus(snapshot);
        NotifyGameplayAvailability();
        OnPropertyChanged(nameof(TurnStatusMessage));
        OnPropertyChanged(nameof(CapturesStatus));
        RefreshClockLabels();
    }

    private string GetGameplayStatus(GameSessionSnapshot snapshot)
    {
        if (snapshot.Status == "Cancelled")
        {
            return GetLocalizedMessage("GameBoard_Result_Cancelled");
        }
        if (snapshot.Status == "Completed")
        {
            string key = MapStone(snapshot.Winner) == MyColor
                ? "GameBoard_Result_Won" : "GameBoard_Result_Lost";
            return GetLocalizedMessage(key) + " "
                + GetLocalizedMessage("GameBoard_Result_" + snapshot.EndReason);
        }
        if (!_isPlaying)
        {
            return GetLocalizedMessage("GameBoard_Status_WaitingOpponent");
        }
        string turn = CurrentTurn == MyColor ? Properties.Resources.GameBoard_Status_YourTurn
            : Properties.Resources.GameBoard_Status_OpponentTurn;
        return snapshot.OpponentConnected ? turn : turn + " "
            + GetLocalizedMessage(GetConnectionStatusResourceKey(snapshot));
    }

    private string FormatClock(long milliseconds, StoneColor color)
    {
        double remaining = milliseconds;
        if (_isPlaying && CurrentTurn == color)
        {
            remaining = GetActiveClockMilliseconds(milliseconds);
        }
        double seconds = TimeSpan.FromMilliseconds(Math.Max(0, remaining)).TotalSeconds;
        TimeSpan time = TimeSpan.FromSeconds(Math.Ceiling(seconds));
        return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}",
            (int)time.TotalMinutes, time.Seconds);
    }

    private double GetActiveClockMilliseconds(long fallbackMilliseconds)
    {
        if (_clockSynchronization.IsSynchronized && _turnDeadlineMilliseconds > 0)
        {
            return _clockSynchronization.GetRemainingMilliseconds(_turnDeadlineMilliseconds);
        }
        return fallbackMilliseconds
            - Stopwatch.GetElapsedTime(_clockReceivedTimestamp).TotalMilliseconds;
    }

    private void OnClockTick(object? sender, EventArgs eventArgs)
    {
        RefreshClockLabels();
    }

    private void RefreshClockLabels()
    {
        OnPropertyChanged(nameof(BlackTimeRemaining));
        OnPropertyChanged(nameof(WhiteTimeRemaining));
    }

    private void PromptLeaveGame()
    {
        if (_isDisposed)
        {
            return;
        }
        if (!GameId.HasValue || _isFinished || _hasPermanentSessionFailure)
        {
            ReturnToMainMenu();
        }
        else
        {
            ShowSurrenderConfirmation(true);
        }
    }

    private void ReturnToMainMenu()
    {
        if (_isDisposed)
        {
            return;
        }
        Dispose();
        _navigationService.NavigateToMainMenu();
    }

    private void NotifyGameplayAvailability()
    {
        OnPropertyChanged(nameof(IsMyTurn));
        OnPropertyChanged(nameof(CanSurrender));
        OnPropertyChanged(nameof(CanLeaveGame));
    }

    private void HandleGameplayFailure(Exception exception)
    {
        Trace.TraceWarning("Gameplay send failed: {0}.", exception.GetType().Name);
        if (exception is RpcException failure && IsPermanentSessionFailure(failure.StatusCode))
        {
            CloseGameSession(failure);
        }
        else
        {
            ActionStatusMessage = GetLocalizedMessage("GameBoard_Action_Recovering");
        }
        // Keep the same action ID: the next connection recovers its durable receipt.
        _sessionAttemptCancellation?.Cancel();
    }

    private async Task RunGameSessionAsync(long gameId, CancellationTokenSource cancellation)
    {
        try
        {
            bool retry = true;
            while (!cancellation.IsCancellationRequested && retry)
            {
                retry = await TryReadSessionAsync(gameId, cancellation.Token);
                if (retry)
                {
                    SessionStatusMessage = GetLocalizedMessage("GameBoard_Action_Recovering");
                    await Task.Delay(ReconnectDelayMilliseconds, cancellation.Token);
                }
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            Trace.TraceInformation("Game session owner stopped.");
        }
        finally
        {
            if (ReferenceEquals(_sessionCancellation, cancellation))
            {
                _sessionCancellation = null;
            }
            cancellation.Dispose();
        }
    }

    private async Task<bool> TryReadSessionAsync(long gameId, CancellationToken cancellationToken)
    {
        using CancellationTokenSource attempt =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _sessionAttemptCancellation = attempt;
        try
        {
            await ReadSessionEventsAsync(gameId, attempt.Token);
        }
        catch (OperationCanceledException) when (attempt.IsCancellationRequested)
        {
            Trace.TraceInformation("Game session read cancelled.");
        }
        catch (RpcException exception)
        {
            if (IsPermanentSessionFailure(exception.StatusCode))
            {
                CloseGameSession(exception);
            }
            else
            {
                SessionStatusMessage = GrpcFailureMessages.GetMessage(exception);
            }
        }
        catch (IOException exception)
        {
            Trace.TraceWarning("Game session transport failed: {0}.", exception.GetType().Name);
        }
        finally
        {
            _gameSessionStream = null;
            _sessionAttemptCancellation = null;
            if (!_isDisposed)
            {
                IsChatEnabled = false;
                NotifyGameplayAvailability();
            }
        }
        return !cancellationToken.IsCancellationRequested && !_hasPermanentSessionFailure;
    }

    private static bool IsPermanentSessionFailure(StatusCode status)
    {
        return status is StatusCode.Unauthenticated or StatusCode.PermissionDenied
            or StatusCode.InvalidArgument or StatusCode.AlreadyExists;
    }

    private void CloseGameSession(RpcException exception)
    {
        _hasPermanentSessionFailure = true;
        _returnToMenuAfterSurrender = false;
        ActionStatusMessage = string.Empty;
        SessionStatusMessage = exception.StatusCode == StatusCode.AlreadyExists
            ? GetLocalizedMessage("GameBoard_Connection_Replaced")
            : GrpcFailureMessages.GetMessage(exception) + " "
                + GetLocalizedMessage("GameBoard_Connection_Closed");
        NotifyGameplayAvailability();
    }

    private async Task ReadSessionEventsAsync(long gameId, CancellationToken cancellationToken)
    {
        _clockSynchronization.BeginConnection();
        await using IGameSessionStream stream = await _gameSessionClient!.ConnectAsync(
            gameId, cancellationToken);
        _gameSessionStream = stream;
        bool restored = false;
        await foreach (GameSessionServerEvent serverEvent in stream.ReadEventsAsync(
            cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ApplyGameServerEvent(serverEvent);
            if (!restored
                && serverEvent.EventCase == GameSessionServerEvent.EventOneofCase.Snapshot)
            {
                restored = true;
                await SendPendingActionAsync(cancellationToken);
            }
        }
    }
}
