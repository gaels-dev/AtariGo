using System.Diagnostics;
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

    private bool _canStartGame;
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
        _roomVersion = room.Version;

        IsHost = navigationContext.IsHost;
        RoomCode = room.InvitationCode;
        TargetCaptures = room.Configuration.CaptureGoal;
        ApplyRoom(room);
        StartGameCommand = new AsyncRelayCommand(
            StartGameAsync, ShowFailure, _lifetimeCancellation.Token);
        LeaveRoomCommand = new AsyncRelayCommand(
            LeaveRoomAsync, ShowFailure, _lifetimeCancellation.Token);
    }

    public bool IsHost { get; }

    public bool IsGuest => !IsHost;

    public string RoomCode { get; }

    public int TargetCaptures { get; }

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

    public bool CanStartGame
    {
        get => _canStartGame;
        private set => SetProperty(ref _canStartGame, value);
    }

    public string StartGameLabel => Properties.Resources.ResourceManager.GetString(
        "PrivateRoom_Btn_StartGame", System.Globalization.CultureInfo.CurrentUICulture)
        ?? "Start game";

    public ICommand StartGameCommand { get; }

    public ICommand LeaveRoomCommand { get; }

    public async Task WatchRoomAsync(PrivateRoomNavigationContext navigationContext)
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
                if (room.State == RoomState.Closed)
                {
                    _navigationService.NavigateToMainMenu();
                    break;
                }

                if (OpenStartedGame(room))
                {
                    break;
                }

                ApplyRoom(room);
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
                ShowFailure(exception);
            }
        }
    }

    public void Dispose()
    {
        if (!_lifetimeCancellation.IsCancellationRequested)
        {
            _lifetimeCancellation.Cancel();
            _lifetimeCancellation.Dispose();
        }
    }

    private bool OpenStartedGame(RoomSnapshot room)
    {
        bool hasStarted = room.State == RoomState.Started && room.GameId > 0;
        if (hasStarted)
        {
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
        await _roomsClient.StartGameAsync(request, _lifetimeCancellation.Token);
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

    private void ApplyRoom(RoomSnapshot room)
    {
        _roomVersion = room.Version;
        RoomParticipant? opponent = room.Participants.FirstOrDefault(
            participant => participant.IsHost != IsHost);
        OpponentName = opponent?.UserName ?? WaitingOpponentName;
        CanStartGame = IsHost && opponent is not null && room.State == RoomState.Waiting;
    }

    private void ShowFailure(Exception exception)
    {
        ErrorMessage = GrpcFailureMessages.GetMessage(exception);
    }
}
