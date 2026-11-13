using System.Diagnostics;
using System.Globalization;
using System.IO;
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
    private static readonly TimeSpan reconnectDelay = TimeSpan.FromSeconds(1);

    private readonly INavigationService _navigationService;
    private readonly IRoomsClient _roomsClient;
    private readonly CancellationTokenSource _lifetimeCancellation = new CancellationTokenSource();
    private readonly int _roomId;
    private RoomSnapshot _room;
    private Task? _observationTask;
    private bool _isBusy;
    private bool _isSynchronized = true;
    private bool _isDisposed;
    private bool _hasOpenedGame;
    private int _selectedCaptureGoal;
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
        _room = navigationContext.Room.Clone();
        _roomId = _room.RoomId;
        IsHost = navigationContext.IsHost;
        SelectedCaptureGoal = _room.Configuration.CaptureGoal;
        ApplyRoom(_room);
        StartGameCommand = new AsyncRelayCommand(
            StartGameAsync, ShowFailure, _lifetimeCancellation.Token);
        LeaveRoomCommand = new AsyncRelayCommand(
            LeaveRoomAsync, ShowFailure, _lifetimeCancellation.Token);
    }

    public RoomInvitationsViewModel? RoomInvitations { get; private set; }

    public bool IsHost { get; }

    public bool IsGuest => !IsHost;

    public string RoomCode => _room.InvitationCode;

    public int TargetCaptures => _room.Configuration.CaptureGoal;

    public int GameTimeSeconds => _room.Configuration.GameTimeSeconds;

    public int TurnTimeSeconds => _room.Configuration.TurnTimeSeconds;

    public IReadOnlyList<int> CaptureGoals { get; } = new[]
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

    public string GameTimeLabel => GetMessage("PrivateRoom_Lbl_GameTimeSeconds");

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

    public ICommand StartGameCommand { get; }

    public ICommand LeaveRoomCommand { get; }

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
        if (_isDisposed)
        {
            return Task.CompletedTask;
        }

        if (_observationTask is null || _observationTask.IsCompleted)
        {
            _observationTask = ObserveRoomAsync(_lifetimeCancellation.Token);
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
        RoomInvitations?.Dispose();
        _lifetimeCancellation.Cancel();
        _lifetimeCancellation.Dispose();
    }

    private async Task ObserveRoomAsync(CancellationToken cancellationToken)
    {
        bool fullSnapshot = false;
        try
        {
            bool finished = HandleRoom(_room);
            while (!finished && !_isDisposed)
            {
                finished = await TryObserveRoomAsync(fullSnapshot, cancellationToken);
                if (!finished)
                {
                    _isSynchronized = false;
                    if (string.IsNullOrEmpty(ErrorMessage))
                    {
                        ErrorMessage = GetMessage("Connection_Err_Unavailable");
                    }

                    NotifyConfigurationState();
                    await Task.Delay(reconnectDelay, cancellationToken);
                    fullSnapshot = true;
                }
            }
        }
        catch (Exception exception)
            when (GrpcCallPolicy.IsRequestedCancellation(exception, cancellationToken))
        {
            Trace.TraceInformation("Room observation stopped with its owner.");
        }
        catch (Exception exception)
        {
            if (!_isDisposed)
            {
                _isSynchronized = false;
                NotifyConfigurationState();
                ShowFailure(exception);
            }
        }
    }

    private async Task<bool> TryObserveRoomAsync(bool fullSnapshot, CancellationToken token)
    {
        bool finished = false;
        try
        {
            var request = new WatchRoomRequest { RoomId = _roomId };
            if (!fullSnapshot)
            {
                request.FromVersion = _room.Version;
            }

            await foreach (RoomSnapshot room in _roomsClient.WatchRoomAsync(request, token))
            {
                token.ThrowIfCancellationRequested();
                finished = HandleRoom(room);
                if (finished)
                {
                    break;
                }
            }
        }
        catch (RpcException exception)
            when (!GrpcCallPolicy.IsRequestedCancellation(exception, token))
        {
            _isSynchronized = false;
            NotifyConfigurationState();
            ShowFailure(exception);
            finished = exception.StatusCode is StatusCode.Unauthenticated
                or StatusCode.PermissionDenied or StatusCode.NotFound;
        }
        catch (IOException exception)
        {
            ShowFailure(exception);
        }

        return finished;
    }

    private async Task StartGameAsync(object? parameter)
    {
        if (!CanStartGame)
        {
            return;
        }

        CancellationToken token = _lifetimeCancellation.Token;
        _isBusy = true;
        ErrorMessage = string.Empty;
        NotifyConfigurationState();
        try
        {
            bool confirmed = await ConfirmConfigurationAsync(token);
            if (confirmed && !_isDisposed)
            {
                var request = new StartGameRequest
                {
                    RoomId = _roomId, ExpectedRoomVersion = _room.Version
                };
                await _roomsClient.StartGameAsync(request, token);
            }
        }
        catch (RpcException exception)
            when (!GrpcCallPolicy.IsRequestedCancellation(exception, token))
        {
            await RecoverStartAsync(exception, token);
        }
        catch (IOException exception)
        {
            await RecoverStartAsync(exception, token);
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

    private async Task<bool> ConfirmConfigurationAsync(CancellationToken token)
    {
        int selectedGoal = SelectedCaptureGoal;
        if (selectedGoal != TargetCaptures)
        {
            RoomConfiguration configuration = _room.Configuration.Clone();
            configuration.CaptureGoal = selectedGoal;
            var request = new UpdateRoomConfigurationRequest
            {
                RoomId = _roomId, Configuration = configuration, ExpectedVersion = _room.Version
            };
            RoomSnapshot confirmed = await _roomsClient.UpdateRoomConfigurationAsync(
                request, token);
            token.ThrowIfCancellationRequested();
            HandleRoom(confirmed);
        }

        return !_isDisposed && _room.State == RoomState.Waiting
            && _room.Configuration.CaptureGoal == selectedGoal
            && _room.Participants.Any(participant => !participant.IsHost);
    }

    private async Task RecoverStartAsync(Exception failure, CancellationToken token)
    {
        if (_isDisposed)
        {
            return;
        }

        _isSynchronized = false;
        string failureMessage = failure is RpcException { StatusCode: StatusCode.Aborted }
            ? GetMessage("PrivateRoom_Err_ConfigurationChanged")
            : GetMessage("PrivateRoom_Err_ConfigurationUnconfirmed");
        ErrorMessage = failureMessage;
        NotifyConfigurationState();
        try
        {
            using CancellationTokenSource timeout =
                CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(GrpcCallPolicy.RequestTimeoutSeconds));
            await foreach (RoomSnapshot room in _roomsClient.WatchRoomAsync(
                new WatchRoomRequest { RoomId = _roomId }, timeout.Token))
            {
                HandleRoom(room);
                if (!_isDisposed)
                {
                    ErrorMessage = failureMessage;
                }

                break;
            }
        }
        catch (Exception exception) when (!token.IsCancellationRequested)
        {
            ShowFailure(exception);
        }
    }

    private async Task LeaveRoomAsync(object? parameter)
    {
        if (!CanLeaveRoom)
        {
            return;
        }

        CancellationToken token = _lifetimeCancellation.Token;
        try
        {
            await _roomsClient.LeaveRoomAsync(_roomId, token);
        }
        catch (RpcException exception) when (exception.StatusCode == StatusCode.NotFound)
        {
            Trace.TraceInformation("The room was already closed.");
        }

        token.ThrowIfCancellationRequested();
        _navigationService.NavigateToMainMenu();
    }

    private bool HandleRoom(RoomSnapshot room)
    {
        if (_isDisposed || room.RoomId != _roomId || room.Version < _room.Version)
        {
            return _isDisposed;
        }

        ApplyRoom(room);
        bool finished = room.State == RoomState.Closed;
        if (finished)
        {
            _navigationService.NavigateToMainMenu();
        }
        else if (room.State == RoomState.Started && room.GameId > 0)
        {
            finished = true;
            if (!_hasOpenedGame)
            {
                _hasOpenedGame = true;
                _navigationService.NavigateToSharedGame(
                    new PrivateRoomNavigationContext(room, IsHost));
            }
        }

        return finished;
    }

    private void ApplyRoom(RoomSnapshot room)
    {
        bool hasDraft = SelectedCaptureGoal != TargetCaptures;
        bool changedConfiguration = room.Configuration.CaptureGoal != TargetCaptures;
        _room = room.Clone();
        if (!_isSynchronized)
        {
            ErrorMessage = string.Empty;
        }

        _isSynchronized = true;
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
            ErrorMessage = GrpcFailureMessages.GetMessage(exception);
        }
    }
}
