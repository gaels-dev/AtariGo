using System;
using System.Windows.Input;

using AtariGo.Client.Commands;
using AtariGo.Client.Models;
using AtariGo.Client.Services;

namespace AtariGo.Client.ViewModels
{
    public class JoinPrivateRoomViewModel : ViewModelBase
    {
        private const int DefaultCaptureGoal = 1;

        private readonly INavigationService _navigationService;
        private string _roomCode;
        private string _errorMessage;

        public JoinPrivateRoomViewModel(
            INavigationService navigationService)
        {
            ArgumentNullException.ThrowIfNull(navigationService);

            _navigationService = navigationService;
            _roomCode = string.Empty;
            _errorMessage = string.Empty;

            JoinCommand = new RelayCommand(JoinRoom);
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

        private void JoinRoom()
        {
            if (string.IsNullOrWhiteSpace(RoomCode))
            {
                ErrorMessage =
                    Properties.Resources.JoinPrivateRoom_Err_EmptyCode;

                return;
            }

            ErrorMessage = string.Empty;

            PrivateRoomNavigationContext navigationContext =
                new PrivateRoomNavigationContext
                {
                    IsHost = false,
                    RoomCode = RoomCode.Trim(),
                    TargetCaptures = DefaultCaptureGoal
                };

            _navigationService.NavigateToPrivateRoomLobby(
                navigationContext);
        }
    }
}