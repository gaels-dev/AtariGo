using System;
using System.Windows.Input;

using AtariGo.Client.Commands;
using AtariGo.Client.Models;
using AtariGo.Client.Services;

namespace AtariGo.Client.ViewModels
{
    public class PrivateRoomLobbyViewModel : ViewModelBase
    {
        private const string WaitingOpponentName = "?";

        private readonly INavigationService _navigationService;
        private readonly bool _isHost;
        private readonly string _roomCode;
        private readonly int _targetCaptures;

        private string _opponentName;
        private bool _hasOpponent;

        public PrivateRoomLobbyViewModel(
            INavigationService navigationService,
            PrivateRoomNavigationContext navigationContext)
        {
            ArgumentNullException.ThrowIfNull(navigationService);
            ArgumentNullException.ThrowIfNull(navigationContext);

            _navigationService = navigationService;
            _isHost = navigationContext.IsHost;
            _roomCode = navigationContext.RoomCode;
            _targetCaptures = navigationContext.TargetCaptures;

            _opponentName = WaitingOpponentName;
            _hasOpponent = false;

            InviteFriendsCommand = new RelayCommand(InviteFriends);
            LeaveRoomCommand = new RelayCommand(LeaveRoom);
            SimulateOpponentCommand = new RelayCommand(SimulateOpponent);
        }

        public bool IsHost
        {
            get
            {
                return _isHost;
            }
        }

        public bool IsGuest
        {
            get
            {
                return !_isHost;
            }
        }

        public string RoomCode
        {
            get
            {
                return _roomCode;
            }
        }

        public int TargetCaptures
        {
            get
            {
                return _targetCaptures;
            }
        }

        public string OpponentName
        {
            get
            {
                return _opponentName;
            }
            private set
            {
                SetProperty(ref _opponentName, value);
            }
        }

        public bool HasOpponent
        {
            get
            {
                return _hasOpponent;
            }
            private set
            {
                SetProperty(ref _hasOpponent, value);
            }
        }

        public ICommand InviteFriendsCommand { get; }

        public ICommand LeaveRoomCommand { get; }

        public ICommand SimulateOpponentCommand { get; }

        private void InviteFriends()
        {
            _navigationService.NavigateToFriendList(
                RoomCode,
                TargetCaptures);
        }

        private void LeaveRoom()
        {
            _navigationService.NavigateToMainMenu();
        }

        private void SimulateOpponent()
        {
            _navigationService.NavigateToGameBoard(
                false,
                TargetCaptures);
        }
    }
}