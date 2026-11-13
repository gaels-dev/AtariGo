using System.Windows.Input;

using AtariGo.Client.Commands;
using AtariGo.Client.Models;
using AtariGo.Client.Services;
using AtariGo.Contracts;

namespace AtariGo.Client.ViewModels
{
    public class JoinPrivateRoomViewModel : ViewModelBase, IDisposable
    {
        private readonly INavigationService _navigationService;
        private readonly IRoomsClient _roomsClient;
        private readonly CancellationTokenSource _lifetimeCancellation =
            new CancellationTokenSource();
        private string _roomCode;
        private string _errorMessage;

        public JoinPrivateRoomViewModel(
            INavigationService navigationService,
            IRoomsClient roomsClient)
        {
            ArgumentNullException.ThrowIfNull(navigationService);

            ArgumentNullException.ThrowIfNull(roomsClient);
            _navigationService = navigationService;
            _roomsClient = roomsClient;
            _roomCode = string.Empty;
            _errorMessage = string.Empty;

            JoinCommand = new AsyncRelayCommand(
                JoinRoomAsync, ShowFailure, _lifetimeCancellation.Token);
            CancelCommand = new RelayCommand(
                _navigationService.NavigateToMainMenu);
        }

        public string RoomCode
        {
            get
            {
                return _roomCode;
            }
            set
            {
                SetProperty(ref _roomCode, value);
            }
        }

        public string ErrorMessage
        {
            get
            {
                return _errorMessage;
            }
            set
            {
                SetProperty(ref _errorMessage, value);
            }
        }

        public ICommand JoinCommand { get; }

        public ICommand CancelCommand { get; }

        public void Dispose()
        {
            _lifetimeCancellation.Cancel();
            _lifetimeCancellation.Dispose();
        }

        private async Task JoinRoomAsync(object? parameter)
        {
            if (string.IsNullOrWhiteSpace(RoomCode))
            {
                ErrorMessage =
                    Properties.Resources.JoinPrivateRoom_Err_EmptyCode;

                return;
            }

            ErrorMessage = string.Empty;

            CancellationToken cancellationToken = _lifetimeCancellation.Token;
            JoinRoomResponse joined = await _roomsClient.JoinRoomAsync(
                RoomCode.Trim(), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            _navigationService.NavigateToPrivateRoomLobby(
                new PrivateRoomNavigationContext(joined.Room, joined.AssignedParticipant.IsHost));
        }

        private void ShowFailure(Exception exception)
        {
            ErrorMessage = GrpcFailureMessages.GetRoomJoinMessage(exception);
        }
    }
}
