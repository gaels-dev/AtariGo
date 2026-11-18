using System.Diagnostics;
using System.Globalization;
using System.Windows.Input;

using AtariGo.Client.Commands;
using AtariGo.Client.Models;
using AtariGo.Client.Services;
using AtariGo.Contracts;

namespace AtariGo.Client.ViewModels;

public sealed class PrivateRoomLobbyViewModel : ViewModelBase, IDisposable
{
    private const string WaitingOpponentName = "?";

    private readonly INavigationService _navigationService;
    private readonly IPrivateRoomSession _session;
    private readonly SynchronizationContext? _uiContext;
    private readonly CancellationTokenSource _lifetimeCancellation = new CancellationTokenSource();
    private readonly int _roomId;
    private RoomSnapshot _room;
    private bool _isBusy;
    private bool _isSynchronized = true;
    private bool _isDisposed;
    private bool _hasNavigated;
    private int _selectedCaptureGoal;
    private string _opponentName = WaitingOpponentName;
    private string _errorMessage = string.Empty;

    public PrivateRoomLobbyViewModel(
        INavigationService navigationService,
        IPrivateRoomSession session,
        PrivateRoomNavigationContext navigationContext)
    {
        ArgumentNullException.ThrowIfNull(navigationService);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(navigationContext);
        _navigationService = navigationService;
        _session = session;
        _uiContext = SynchronizationContext.Current;
        _room = session.Room;
        _roomId = _room.RoomId;
        IsHost = navigationContext.IsHost;
        SelectedCaptureGoal = _room.Configuration.CaptureGoal;
        ApplyRoom(_room);
        StartGameCommand = new AsyncRelayCommand(
            StartGameAsync, ShowFailure, _lifetimeCancellation.Token);
        LeaveRoomCommand = new AsyncRelayCommand(
            LeaveRoomAsync, ShowFailure, _lifetimeCancellation.Token);
        _session.Updated += HandleSessionUpdate;
    }

    public RoomInvitationsViewModel? RoomInvitations
    {
        get; private set;
    }

    public bool IsHost
    {
        get;
    }

    public bool IsGuest => !IsHost;

    public string RoomCode => _room.InvitationCode;

    public int TargetCaptures => _room.Configuration.CaptureGoal;

    public int GameTimeSeconds => _room.Configuration.GameTimeSeconds;

    public int TurnTimeSeconds => _room.Configuration.TurnTimeSeconds;

    public IReadOnlyList<int> CaptureGoals
    {
        get;
    } = new[]
    {
        GameRules.DefaultCaptureGoal,
        GameRules.IntermediateCaptureGoal,
        GameRules.ExtendedCaptureGoal
    };

    public int SelectedCaptureGoal
    {
        get => _selectedCaptureGoal;
        set
        {
            if (SetProperty(ref _selectedCaptureGoal, value))
            {
                NotifyConfigurationState();
            }
        }
    }

    public bool CanEditConfiguration => IsHost && CanLeaveRoom && _isSynchronized
        && _room.State == RoomState.Waiting;

    public bool CanStartGame => CanEditConfiguration && CaptureGoals.Contains(SelectedCaptureGoal)
        && _room.Participants.Any(participant => !participant.IsHost);

    public bool CanLeaveRoom => !_isBusy && !_isDisposed;

    public string CaptureGoalLabel => GetMessage("PrivateRoom_Lbl_CaptureGoal");

    public string TurnTimeLabel => GetMessage("PrivateRoom_Lbl_TurnTimeSeconds");

    public string StartGameLabel => GetMessage("PrivateRoom_Btn_StartGame");

    public string OpponentName
    {
        get => _opponentName;
        private set => SetProperty(ref _opponentName, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public ICommand StartGameCommand
    {
        get;
    }

    public ICommand LeaveRoomCommand
    {
        get;
    }

    public void AttachInvitations(RoomInvitationsViewModel invitations)
    {
        ArgumentNullException.ThrowIfNull(invitations);
        RoomInvitations?.Dispose();
        RoomInvitations = invitations;
        UpdateInvitationAvailability();
        OnPropertyChanged(nameof(RoomInvitations));
    }

    public Task WatchRoomAsync()
    {
        return _isDisposed ? Task.CompletedTask : _session.WatchAsync(_lifetimeCancellation.Token);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }
        _isDisposed = true;
        _session.Updated -= HandleSessionUpdate;
        RoomInvitations?.Dispose();
        _lifetimeCancellation.Cancel();
        _session.Dispose();
        _lifetimeCancellation.Dispose();
    }

    private async Task StartGameAsync(object? parameter)
    {
        if (!CanStartGame)
        {
            return;
        }
        _isBusy = true;
        ErrorMessage = string.Empty;
        NotifyConfigurationState();
        try
        {
            await _session.StartGameAsync(SelectedCaptureGoal, _lifetimeCancellation.Token);
        }
        finally
        {
            _isBusy = false;
            if (!_isDisposed)
            {
                NotifyConfigurationState();
            }
        }
    }

    private async Task LeaveRoomAsync(object? parameter)
    {
        if (!CanLeaveRoom)
        {
            return;
        }
        bool left = await _session.LeaveAsync(_lifetimeCancellation.Token);
        if (left && !_isDisposed)
        {
            _navigationService.NavigateToMainMenu();
        }
    }

    private void HandleSessionUpdate(PrivateRoomSessionUpdate update)
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
        if (state is PrivateRoomSessionUpdate update)
        {
            ApplySessionUpdate(update);
        }
    }

    private void ApplySessionUpdate(PrivateRoomSessionUpdate update)
    {
        if (_isDisposed || update.Room.RoomId != _roomId || update.Room.Version < _room.Version)
        {
            return;
        }
        _isSynchronized = update.IsSynchronized;
        ApplyRoom(update.Room);
        ErrorMessage = GetFailureMessage(update);
        NotifyConfigurationState();
        NavigateForRoom(update.Room);
    }

    private void NavigateForRoom(RoomSnapshot room)
    {
        if (_hasNavigated)
        {
            return;
        }
        if (room.State == RoomState.Closed)
        {
            _hasNavigated = true;
            _navigationService.NavigateToMainMenu();
        }
        else if (room.State == RoomState.Started && room.GameId > 0)
        {
            _hasNavigated = true;
            _navigationService.NavigateToSharedGame(new PrivateRoomNavigationContext(room, IsHost));
        }
    }

    private static string GetFailureMessage(PrivateRoomSessionUpdate update)
    {
        if (update.IsStartRecovery)
        {
            string recoveryKey = update.Failure == ClientFailureReason.VersionConflict
                ? "PrivateRoom_Err_ConfigurationChanged"
                : "PrivateRoom_Err_ConfigurationUnconfirmed";
            return GetMessage(recoveryKey);
        }
        if (update.Failure == ClientFailureReason.None)
        {
            return string.Empty;
        }
        string key = update.Failure switch
        {
            ClientFailureReason.Timeout => "Connection_Err_Timeout",
            ClientFailureReason.Unavailable => "Connection_Err_Unavailable",
            ClientFailureReason.SessionExpired => "Connection_Err_SessionExpired",
            ClientFailureReason.Cancelled => "Connection_Err_Cancelled",
            _ => "Connection_Err_RequestFailed"
        };
        return GetMessage(key);
    }

    private void ApplyRoom(RoomSnapshot room)
    {
        bool hasDraft = SelectedCaptureGoal != TargetCaptures;
        bool changedConfiguration = room.Configuration.CaptureGoal != TargetCaptures;
        _room = room.Clone();
        if (!hasDraft || changedConfiguration)
        {
            SelectedCaptureGoal = TargetCaptures;
        }

        RoomParticipant? opponent = room.Participants.FirstOrDefault(
            participant => participant.IsHost != IsHost);
        OpponentName = opponent?.UserName ?? WaitingOpponentName;
        OnPropertyChanged(nameof(TargetCaptures));
        OnPropertyChanged(nameof(GameTimeSeconds));
        OnPropertyChanged(nameof(TurnTimeSeconds));
        NotifyConfigurationState();
    }

    private void UpdateInvitationAvailability()
    {
        if (RoomInvitations is not null)
        {
            RoomInvitations.IsRoomAvailable = CanLeaveRoom && _isSynchronized
                && _room.State == RoomState.Waiting
                && !_room.Participants.Any(participant => !participant.IsHost);
        }
    }

    private void NotifyConfigurationState()
    {
        OnPropertyChanged(nameof(CanEditConfiguration));
        OnPropertyChanged(nameof(CanStartGame));
        OnPropertyChanged(nameof(CanLeaveRoom));
        UpdateInvitationAvailability();
    }

    private static string GetMessage(string resourceKey)
    {
        return Properties.Resources.ResourceManager.GetString(
            resourceKey, CultureInfo.CurrentUICulture) ?? resourceKey;
    }

    private void ShowFailure(Exception exception)
    {
        if (!_isDisposed)
        {
            Trace.TraceWarning("Room command failed: {0}.", exception.GetType().Name);
            ErrorMessage = GetMessage("Connection_Err_RequestFailed");
        }
    }
}
