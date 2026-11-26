using System.Diagnostics;
using System.Globalization;
using System.Windows.Input;
using System.Windows.Threading;

using AtariGo.Client.Commands;
using AtariGo.Client.Models;
using AtariGo.Client.Services;
using AtariGo.Client.ViewModels.Dialogs;
using AtariGo.Contracts;

namespace AtariGo.Client.ViewModels;

public partial class GameBoardViewModel
{
    private const int ClockRefreshMilliseconds = 250;
    private readonly GameClockSynchronization _clockSynchronization =
        new GameClockSynchronization(TimeProvider.System);
    private long _turnDeadlineMilliseconds;
    private readonly DispatcherTimer _clockTimer = new DispatcherTimer();
    private bool _isPlaying;
    private bool _isFinished;
    private bool _hasPermanentSessionFailure;
    private bool _returnToMenuAfterSurrender;
    private bool _hasShownGameEndDialog;
    private bool _isLeaving;
    private long _blackClockMilliseconds;
    private long _whiteClockMilliseconds;
    private long _clockReceivedTimestamp;
    private bool _hasPendingAction;
    private bool _isSessionConnected;
    private bool _turnPaused;
    private bool _isConnectionAlertVisible;
    private bool _connectionAttemptFailed;
    private bool _reconnectionAttemptStarted;
    private long _opponentReconnectDeadlineMilliseconds;
    private string _connectionAlertTitle = string.Empty;
    private string _connectionAlertDescription = string.Empty;
    private string _actionStatusMessage = string.Empty;

    public ICommand PlaceStoneCommand { get; private set; } = null!;

    public ICommand RetryConnectionCommand { get; private set; } = null!;

    public ICommand SubmitSurrenderCommand { get; private set; } = null!;

    public string ActionStatusMessage
    {
        get => _actionStatusMessage;
        private set => SetProperty(ref _actionStatusMessage, value);
    }

    public bool IsConnectionAlertVisible
    {
        get => _isConnectionAlertVisible;
        private set
        {
            if (SetProperty(ref _isConnectionAlertVisible, value))
            {
                OnPropertyChanged(nameof(IsGameplayInteractive));
            }
        }
    }

    public bool IsGameplayInteractive => !IsConnectionAlertVisible;

    public string RetryConnectionButtonLabel =>
        GetLocalizedMessage("GameBoard_Connection_Retry");

    public string ConnectionAlertTitle
    {
        get => _connectionAlertTitle;
        private set => SetProperty(ref _connectionAlertTitle, value);
    }

    public string ConnectionAlertDescription
    {
        get => _connectionAlertDescription;
        private set => SetProperty(ref _connectionAlertDescription, value);
    }

    public string OpponentReconnectionStatus
    {
        get
        {
            if (_opponentReconnectDeadlineMilliseconds <= 0 || !_turnPaused)
            {
                return string.Empty;
            }
            double remaining = _clockSynchronization.IsSynchronized
                ? _clockSynchronization.GetRemainingMilliseconds(
                    _opponentReconnectDeadlineMilliseconds)
                : 0;
            TimeSpan duration = TimeSpan.FromMilliseconds(remaining);
            string time = string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}",
                (int)duration.TotalMinutes, duration.Seconds);
            return string.Format(CultureInfo.CurrentCulture,
                GetLocalizedMessage("GameBoard_Status_ReconnectionCountdown"), time);
        }
    }

    public bool CanSurrender => !_isDisposed && !_isFinished && !_hasPermanentSessionFailure
        && _isSessionConnected && !_hasPendingAction;

    public bool CanLeaveGame => !_isDisposed && !_isLeaving && (!GameId.HasValue || _isFinished
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
        RetryConnectionCommand = new RelayCommand(RetryConnection);
        SubmitSurrenderCommand = new AsyncRelayCommand(_ => SurrenderAsync(),
            HandleGameplayFailure, _lifetimeCancellation.Token);
        _clockTimer.Interval = TimeSpan.FromMilliseconds(ClockRefreshMilliseconds);
        _clockTimer.Tick += OnClockTick;
        _clockTimer.Start();
    }

    private void RetryConnection()
    {
        _connectionAttemptFailed = false;
        _reconnectionAttemptStarted = true;
        UpdateConnectionAlert();
        _session?.RetryConnection();
    }

    private void UpdateConnectionAlert()
    {
        string titleKey = _connectionAttemptFailed
            ? "GameBoard_Connection_ErrorTitle" : "GameBoard_Connection_LostTitle";
        string descriptionKey = _connectionAttemptFailed
            ? "GameBoard_Connection_ErrorDescription"
            : "GameBoard_Connection_LostDescription";
        ConnectionAlertTitle = GetLocalizedMessage(titleKey);
        ConnectionAlertDescription = GetLocalizedMessage(descriptionKey);
        IsConnectionAlertVisible = true;
    }

    private async Task PlaceStoneAsync(object? parameter)
    {
        if (parameter is not BoardCellViewModel cell || !IsMyTurn
            || cell.Stone != StoneColor.None)
        {
            return;
        }
        await _session!.SubmitMoveAsync(cell.Row, cell.Column, _lifetimeCancellation.Token);
    }

    private async Task SurrenderAsync()
    {
        if (!CanSurrender || GameId is null)
        {
            return;
        }
        await _session!.SurrenderAsync(_lifetimeCancellation.Token);
    }

    private void ApplyActionResult(GameActionResponse result, bool isSurrender)
    {
        bool shouldReturn = _returnToMenuAfterSurrender && isSurrender && result.IsAccepted;
        _returnToMenuAfterSurrender = false;
        ActionStatusMessage = result.IsAccepted ? string.Empty
            : GetLocalizedMessage("GameBoard_Action_" + result.Rejection);
        NotifyGameplayAvailability();
        if (shouldReturn)
        {
            _isFinished = true;
            ReturnToMainMenu();
        }
    }

    private void ApplyGameplaySnapshot(GameSessionSnapshot snapshot)
    {
        _isPlaying = snapshot.Status == "InProgress";
        _isFinished = snapshot.Status == "Completed" || snapshot.Status == "Cancelled";
        _turnPaused = snapshot.TurnPaused;
        _opponentReconnectDeadlineMilliseconds =
            snapshot.OpponentReconnectDeadlineUnixMilliseconds;
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
        if (_isFinished && !_hasShownGameEndDialog)
        {
            _hasShownGameEndDialog = true;
            ShowGameEndDialog(snapshot);
        }
    }

    private void ShowGameEndDialog(GameSessionSnapshot snapshot)
    {
        string title = GetLocalizedMessage("GameBoard_GameOver_Title");
        if (snapshot.Status == "Cancelled")
        {
            ShowResultDialog(title, GetLocalizedMessage(
                "GameBoard_Result_" + snapshot.EndReason));
            return;
        }
        string outcomeKey = "GameBoard_Result_Lost";
        if (MapStone(snapshot.Winner) == MyColor)
        {
            outcomeKey = "GameBoard_Result_Won";
        }

        string reasonKey = "GameBoard_Result_" + snapshot.EndReason;
        string message = GetLocalizedMessage(outcomeKey) + " " + GetLocalizedMessage(reasonKey);
        ShowResultDialog(title, message);
    }

    private void ShowResultDialog(string title, string message)
    {
        string actionButtonText = GetLocalizedMessage("GameBoard_GameOver_Action");

        GameOverDialogViewModel dialog = new GameOverDialogViewModel(title, message, actionButtonText);
        dialog.DialogClosed += OnGameEndDialogClosed;
        _navigationService.OpenDialog(dialog);
    }

    private void OnGameEndDialogClosed(bool? result)
    {
        ReturnToMainMenu();
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
        if (snapshot.OpponentDisconnected)
        {
            return GetLocalizedMessage(GetConnectionStatusResourceKey(snapshot));
        }
        if (snapshot.TurnConfirmationPending)
        {
            return GetLocalizedMessage("GameBoard_Status_ConfirmingTurn");
        }
        if (snapshot.TurnPaused)
        {
            return GetLocalizedMessage(GetConnectionStatusResourceKey(snapshot));
        }
        string turn = CurrentTurn == MyColor ? Properties.Resources.GameBoard_Status_YourTurn
            : Properties.Resources.GameBoard_Status_OpponentTurn;
        return snapshot.OpponentConnected ? turn : turn + " "
            + GetLocalizedMessage(GetConnectionStatusResourceKey(snapshot));
    }

    private string FormatClock(long milliseconds, StoneColor color)
    {
        double remaining = milliseconds;
        if (_isPlaying && !_turnPaused && CurrentTurn == color)
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
        OnPropertyChanged(nameof(OpponentReconnectionStatus));
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
        if (_isDisposed || _isLeaving)
        {
            return;
        }
        _isLeaving = true;
        IsChatEnabled = false;
        NotifyGameplayAvailability();
        _returnToMenuCommand.Execute(null);
    }

    private async Task ReturnToMainMenuAsync(object? _)
    {
        try
        {
            if (_session is not null && _isFinished)
            {
                await _session.CloseAsync(_lifetimeCancellation.Token);
            }
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            Trace.TraceError("Game session shutdown failed. Error: {0}.",
                exception.GetType().Name);
        }
        if (!_isDisposed)
        {
            Dispose();
            _navigationService.NavigateToMainMenu();
        }
    }

    private void NotifyGameplayAvailability()
    {
        OnPropertyChanged(nameof(IsMyTurn));
        OnPropertyChanged(nameof(CanSurrender));
        OnPropertyChanged(nameof(CanLeaveGame));
    }

    private void HandleGameplayFailure(Exception exception)
    {
        if (!_isDisposed)
        {
            Trace.TraceWarning("Gameplay command failed: {0}.", exception.GetType().Name);
            ActionStatusMessage = GetLocalizedMessage("Connection_Err_RequestFailed");
        }
    }

    private void HandleSessionUpdate(GameplaySessionUpdate update)
    {
        if (_uiContext is not null && SynchronizationContext.Current != _uiContext)
        {
            _uiContext.Send(ApplyDispatchedUpdate, update);
        }
        else
        {
            ApplySessionUpdate(update);
        }
    }

    private void ApplyDispatchedUpdate(object? state)
    {
        if (state is GameplaySessionUpdate update)
        {
            ApplySessionUpdate(update);
        }
    }

    private void ApplySessionUpdate(GameplaySessionUpdate update)
    {
        if (_isDisposed || _isLeaving)
        {
            return;
        }
        _hasPendingAction = update.HasPendingAction;
        _isSessionConnected = update.State == GameplayConnectionState.Connected;
        if (update.ConnectionStarted && IsConnectionAlertVisible)
        {
            _reconnectionAttemptStarted = true;
        }
        if (_isSessionConnected)
        {
            IsConnectionAlertVisible = false;
            _connectionAttemptFailed = false;
            _reconnectionAttemptStarted = false;
        }
        else if (update.State != GameplayConnectionState.Closed
            && (update.Failure != ClientFailureReason.None || IsConnectionAlertVisible))
        {
            _connectionAttemptFailed |= update.Failure != ClientFailureReason.None
                && _reconnectionAttemptStarted;
            UpdateConnectionAlert();
        }
        IsChatEnabled = _isSessionConnected && !_isGuest;
        if (update.ConnectionStarted)
        {
            _clockSynchronization.BeginConnection();
        }
        if (update.State == GameplayConnectionState.Closed)
        {
            ApplyClosedSession(update.Failure);
        }
        else if (update.InvalidSnapshot)
        {
            SessionStatusMessage = GetLocalizedMessage("GameBoard_Status_InvalidSnapshot");
        }
        else if (update.ServerEvent is not null)
        {
            ApplyGameServerEvent(update);
        }
        else
        {
            ApplyConnectionStatus(update);
        }
        if (!_isDisposed)
        {
            if (update.ChatSendFailed)
            {
                ChatStatusMessage = GetLocalizedMessage("GameBoard_Chat_Err_SendFailed");
            }
            NotifyGameplayAvailability();
        }
    }

    private void ApplyConnectionStatus(GameplaySessionUpdate update)
    {
        if (update.State != GameplayConnectionState.Connected)
        {
            string key = update.State == GameplayConnectionState.Connecting
                ? "GameBoard_Status_Connecting" : "GameBoard_Action_Recovering";
            SessionStatusMessage = GetLocalizedMessage(key);
        }
        if (update.HasPendingAction)
        {
            string key = _isSessionConnected
                ? "GameBoard_Action_Pending" : "GameBoard_Action_Recovering";
            ActionStatusMessage = GetLocalizedMessage(key);
        }
    }

    private void ApplyClosedSession(ClientFailureReason failure)
    {
        IsConnectionAlertVisible = false;
        if (failure == ClientFailureReason.ReconnectionExpired)
        {
            _isFinished = true;
            _hasShownGameEndDialog = true;
            ShowResultDialog(GetLocalizedMessage("GameBoard_GameOver_Title"),
                GetLocalizedMessage("GameBoard_Connection_ReconnectionExpired"));
            return;
        }
        _hasPermanentSessionFailure = true;
        _returnToMenuAfterSurrender = false;
        ActionStatusMessage = string.Empty;
        if (failure == ClientFailureReason.ConnectionReplaced)
        {
            SessionStatusMessage = GetLocalizedMessage("GameBoard_Connection_Replaced");
        }
        else
        {
            string key = failure == ClientFailureReason.SessionExpired
                ? "Connection_Err_SessionExpired" : "Connection_Err_RequestFailed";
            SessionStatusMessage = GetLocalizedMessage(key) + " "
                + GetLocalizedMessage("GameBoard_Connection_Closed");
        }
    }
}
