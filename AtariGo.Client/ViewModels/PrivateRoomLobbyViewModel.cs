using System.Diagnostics;
using System.Globalization;
using System.Windows.Input;

using Grpc.Core;

using AtariGo.Client.Commands;
using AtariGo.Client.Models;
using AtariGo.Client.Services;
using AtariGo.Contracts;

namespace AtariGo.Client.ViewModels;

public sealed class PrivateRoomLobbyViewModel : ViewModelBase, IDisposable
{
    private const string WaitingOpponentName = "?";

    private readonly INavigationService _navigationService;
    private readonly IRoomsClient _roomsClient;
    private readonly CancellationTokenSource _lifetimeCancellation = new CancellationTokenSource();
    private readonly int _roomId;
    private int _roomVersion;

    private RoomSnapshot _room;
    private Task? _observationTask;
    private bool _isBusy;
    private bool _requiresRefresh;
    private bool _isDisposed;
    private bool _hasOpenedGame;
    private int _selectedCaptureGoal;
    private int _targetCaptures;
    private int _gameTimeSeconds;
    private int _turnTimeSeconds;
    private string _opponentName = WaitingOpponentName;
    private string _errorMessage = string.Empty;

    public PrivateRoomLobbyViewModel(
        INavigationService navigationService,
        IRoomsClient roomsClient,
        PrivateRoomNavigationContext navigationContext)
    {
        ArgumentNullException.ThrowIfNull(navigationService);
        ArgumentNullException.ThrowIfNull(roomsClient);
        ArgumentNullException.ThrowIfNull(navigationContext);
        _navigationService = navigationService;
        _roomsClient = roomsClient;
        RoomSnapshot room = navigationContext.Room;
        _roomId = room.RoomId;

        IsHost = navigationContext.IsHost;
        RoomCode = room.InvitationCode;
        _room = room.Clone();
        _roomVersion = 0;
        ApplyRoom(room);
        StartGameCommand = new AsyncRelayCommand(
            StartGameAsync, ShowFailure, _lifetimeCancellation.Token);
        LeaveRoomCommand = new AsyncRelayCommand(
            LeaveRoomAsync, ShowFailure, _lifetimeCancellation.Token);
        SaveConfigurationCommand = new AsyncRelayCommand(
            SaveConfigurationAsync, ShowFailure, _lifetimeCancellation.Token);
        DiscardConfigurationCommand = new RelayCommand(DiscardConfiguration);
        RefreshConfigurationCommand = new AsyncRelayCommand(
            RefreshConfigurationAsync, ShowFailure, _lifetimeCancellation.Token);
    }

    public bool IsHost { get; }

    public bool IsGuest => !IsHost;

    public string RoomCode { get; }

    public IReadOnlyList<int> CaptureGoals { get; } = new[]
    {
        GameRules.DefaultCaptureGoal,
        GameRules.IntermediateCaptureGoal,
        GameRules.ExtendedCaptureGoal
    };

    public int TargetCaptures
    {
        get => _targetCaptures;
        private set => SetProperty(ref _targetCaptures, value);
    }

    public int GameTimeSeconds
    {
        get => _gameTimeSeconds;
        private set => SetProperty(ref _gameTimeSeconds, value);
    }

    public int TurnTimeSeconds
    {
        get => _turnTimeSeconds;
        private set => SetProperty(ref _turnTimeSeconds, value);
    }

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

    public bool HasPendingChanges => SelectedCaptureGoal != TargetCaptures;

    public bool CanEditConfiguration => IsHost && !_isBusy && !_requiresRefresh
        && _room.State == RoomState.Waiting && !_isDisposed;

    public bool CanSaveConfiguration => CanEditConfiguration && HasPendingChanges
        && CaptureGoals.Contains(SelectedCaptureGoal);

    public bool CanDiscardConfiguration => CanEditConfiguration && HasPendingChanges;

    public bool CanRefreshConfiguration => !_isBusy && !_isDisposed;

    public string SaveConfigurationLabel => GetMessage("PrivateRoom_Btn_SaveConfiguration");

    public string DiscardConfigurationLabel => GetMessage("PrivateRoom_Btn_DiscardConfiguration");

    public string RefreshConfigurationLabel => GetMessage("PrivateRoom_Btn_RefreshConfiguration");

    public string CaptureGoalLabel => GetMessage("PrivateRoom_Lbl_CaptureGoal");

    public string GameTimeLabel => GetMessage("PrivateRoom_Lbl_GameTimeSeconds");

    public string TurnTimeLabel => GetMessage("PrivateRoom_Lbl_TurnTimeSeconds");

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

    public bool CanStartGame => CanEditConfiguration && !HasPendingChanges
        && _room.Participants.Any(participant => !participant.IsHost);

    public string StartGameLabel => Properties.Resources.ResourceManager.GetString(
        "PrivateRoom_Btn_StartGame", System.Globalization.CultureInfo.CurrentUICulture)
        ?? "Start game";

    public ICommand StartGameCommand { get; }

    public ICommand LeaveRoomCommand { get; }

    public ICommand SaveConfigurationCommand { get; }

    public ICommand DiscardConfigurationCommand { get; }

    public ICommand RefreshConfigurationCommand { get; }

    public Task WatchRoomAsync(PrivateRoomNavigationContext navigationContext)
    {
        if (_isDisposed)
        {
            return Task.CompletedTask;
        }

        if (_observationTask is null || _observationTask.IsCompleted)
        {
            _observationTask = ObserveRoomAsync(navigationContext);
        }

        return _observationTask;
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _lifetimeCancellation.Cancel();
        _lifetimeCancellation.Dispose();
    }

    private async Task ObserveRoomAsync(PrivateRoomNavigationContext navigationContext)
    {
        if (OpenStartedGame(navigationContext.Room))
        {
            return;
        }

        CancellationToken cancellationToken = _lifetimeCancellation.Token;
        var request = new WatchRoomRequest { RoomId = _roomId, FromVersion = _roomVersion };
        try
        {
            await foreach (RoomSnapshot room in _roomsClient.WatchRoomAsync(
                request, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (HandleRoom(room))
                {
                    break;
                }
            }
        }
        catch (Exception exception)
            when (GrpcCallPolicy.IsRequestedCancellation(exception, cancellationToken))
        {
            Trace.TraceInformation("Room observation was cancelled.");
        }
        catch (Exception exception)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                _requiresRefresh = true;
                NotifyConfigurationState();
                ShowFailure(exception);
            }
        }
    }

    private bool OpenStartedGame(RoomSnapshot room)
    {
        bool hasStarted = room.State == RoomState.Started && room.GameId > 0;
        if (hasStarted && !_hasOpenedGame && !_isDisposed)
        {
            _hasOpenedGame = true;
            _navigationService.NavigateToSharedGame(new PrivateRoomNavigationContext(room, IsHost));
        }

        return hasStarted;
    }

    private async Task StartGameAsync(object? parameter)
    {
        if (!CanStartGame)
        {
            return;
        }

        ErrorMessage = string.Empty;
        var request = new StartGameRequest
        {
            RoomId = _roomId,
            ExpectedRoomVersion = _roomVersion
        };
        _isBusy = true;
        NotifyConfigurationState();
        try
        {
            await _roomsClient.StartGameAsync(request, _lifetimeCancellation.Token);
        }
        catch (RpcException exception) when (exception.StatusCode == StatusCode.Aborted)
        {
            await RefreshRoomAsync(_lifetimeCancellation.Token);
            if (!_isDisposed)
            {
                ErrorMessage = GetMessage("PrivateRoom_Err_ConfigurationChanged");
            }
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
        CancellationToken cancellationToken = _lifetimeCancellation.Token;
        try
        {
            await _roomsClient.LeaveRoomAsync(_roomId, cancellationToken);
        }
        catch (RpcException exception) when (exception.StatusCode == StatusCode.NotFound)
        {
            Trace.TraceInformation("The room was already closed.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        _navigationService.NavigateToMainMenu();
    }

    private bool HandleRoom(RoomSnapshot room)
    {
        if (_isDisposed || room.RoomId != _roomId || room.Version < _roomVersion)
        {
            return _isDisposed;
        }

        ApplyRoom(room);
        bool isClosed = room.State == RoomState.Closed;
        if (isClosed)
        {
            _navigationService.NavigateToMainMenu();
        }

        return isClosed || OpenStartedGame(room);
    }

    private void ApplyRoom(RoomSnapshot room)
    {
        if (_isDisposed || room.RoomId != _roomId || room.Version < _roomVersion)
        {
            return;
        }

        bool changed = room.Version > _roomVersion;
        bool invalidatedDraft = changed && HasPendingChanges && !_isBusy;
        _room = room.Clone();
        _roomVersion = room.Version;
        TargetCaptures = room.Configuration.CaptureGoal;
        GameTimeSeconds = room.Configuration.GameTimeSeconds;
        TurnTimeSeconds = room.Configuration.TurnTimeSeconds;
        if (changed)
        {
            SelectedCaptureGoal = TargetCaptures;
        }

        RoomParticipant? opponent = room.Participants.FirstOrDefault(
            participant => participant.IsHost != IsHost);
        OpponentName = opponent?.UserName ?? WaitingOpponentName;
        if (invalidatedDraft)
        {
            ErrorMessage = GetMessage("PrivateRoom_Err_ConfigurationChanged");
        }

        NotifyConfigurationState();
    }

    private void NotifyConfigurationState()
    {
        OnPropertyChanged(nameof(HasPendingChanges));
        OnPropertyChanged(nameof(CanEditConfiguration));
        OnPropertyChanged(nameof(CanSaveConfiguration));
        OnPropertyChanged(nameof(CanDiscardConfiguration));
        OnPropertyChanged(nameof(CanRefreshConfiguration));
        OnPropertyChanged(nameof(CanStartGame));
    }

    private void DiscardConfiguration()
    {
        if (CanDiscardConfiguration)
        {
            SelectedCaptureGoal = TargetCaptures;
            ErrorMessage = string.Empty;
        }
    }

    private async Task SaveConfigurationAsync(object? parameter)
    {
        if (!CanSaveConfiguration)
        {
            return;
        }

        CancellationToken cancellationToken = _lifetimeCancellation.Token;
        RoomConfiguration configuration = _room.Configuration.Clone();
        configuration.CaptureGoal = SelectedCaptureGoal;
        var request = new UpdateRoomConfigurationRequest
        {
            RoomId = _roomId,
            Configuration = configuration,
            ExpectedVersion = _roomVersion
        };
        _isBusy = true;
        ErrorMessage = string.Empty;
        NotifyConfigurationState();
        try
        {
            RoomSnapshot confirmed = await _roomsClient.UpdateRoomConfigurationAsync(
                request, cancellationToken);
            HandleRoom(confirmed);
        }
        catch (RpcException exception) when (exception.StatusCode == StatusCode.Aborted)
        {
            await RefreshRoomAsync(cancellationToken);
            if (!_isDisposed)
            {
                ErrorMessage = GetMessage("PrivateRoom_Err_ConfigurationChanged");
            }
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            _requiresRefresh = true;
            Trace.TraceWarning("Configuration update unconfirmed: {0}.", exception.GetType().Name);
            if (!_isDisposed)
            {
                ErrorMessage = GetMessage("PrivateRoom_Err_ConfigurationUnconfirmed");
            }
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

    private async Task RefreshConfigurationAsync(object? parameter)
    {
        if (!CanRefreshConfiguration)
        {
            return;
        }

        _isBusy = true;
        ErrorMessage = string.Empty;
        NotifyConfigurationState();
        try
        {
            await RefreshRoomAsync(_lifetimeCancellation.Token);
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

    private async Task RefreshRoomAsync(CancellationToken cancellationToken)
    {
        // A fresh observation returns the current snapshot without replaying the mutation.
        _requiresRefresh = true;
        using CancellationTokenSource refreshCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        refreshCancellation.CancelAfter(TimeSpan.FromSeconds(GrpcCallPolicy.RequestTimeoutSeconds));
        var request = new WatchRoomRequest { RoomId = _roomId };
        await foreach (RoomSnapshot room in _roomsClient.WatchRoomAsync(
            request, refreshCancellation.Token))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_isDisposed)
            {
                break;
            }

            if (HandleRoom(room))
            {
                break;
            }

            SelectedCaptureGoal = TargetCaptures;
            _requiresRefresh = false;
            NotifyConfigurationState();
            ResumeObservation();
            break;
        }
    }

    private void ResumeObservation()
    {
        if (!_isDisposed && _room.State == RoomState.Waiting
            && (_observationTask is null || _observationTask.IsCompleted))
        {
            // This ViewModel owns the task and cancels it when navigation disposes the lobby.
            _observationTask = ObserveRoomAsync(new PrivateRoomNavigationContext(_room, IsHost));
        }
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
            ErrorMessage = GrpcFailureMessages.GetMessage(exception);
        }
    }
}
