using System;
using System.Collections.ObjectModel;
using System.Windows.Input;

using AtariGo.Client.Commands;
using AtariGo.Client.Models;
using AtariGo.Client.Services;

namespace AtariGo.Client.ViewModels
{
    public class FriendListViewModel : ViewModelBase
    {
        private const string ExampleFriendOne = "KitsunePlayer";
        private const string ExampleFriendTwo = "GoMaster";
        private const string ExampleFriendThree = "AtariNinja";

        private readonly INavigationService _navigationService;
        private readonly string _roomCode;
        private readonly int _targetCaptures;
        private string _statusMessage;

        public FriendListViewModel(
            INavigationService navigationService,
            string roomCode,
            int targetCaptures)
        {
            ArgumentNullException.ThrowIfNull(navigationService);

            _navigationService = navigationService;
            _roomCode = roomCode;
            _targetCaptures = targetCaptures;
            _statusMessage = string.Empty;

            Friends = new ObservableCollection<FriendItemViewModel>
            {
                new FriendItemViewModel(ExampleFriendOne, true),
                new FriendItemViewModel(ExampleFriendTwo, false),
                new FriendItemViewModel(ExampleFriendThree, true)
            };

            InviteFriendCommand = new RelayCommand(InviteFriend);
            RemoveFriendCommand = new RelayCommand(RemoveFriend);
            BackCommand = new RelayCommand(GoBack);
        }

        public ObservableCollection<FriendItemViewModel> Friends { get; }

        public string StatusMessage
        {
            get
            {
                return _statusMessage;
            }
            private set
            {
                SetProperty(ref _statusMessage, value);
            }
        }

        public ICommand InviteFriendCommand { get; }

        public ICommand RemoveFriendCommand { get; }

        public ICommand BackCommand { get; }

        private void InviteFriend(object? parameter)
        {
            if (parameter is not FriendItemViewModel friend)
            {
                return;
            }

            if (!friend.IsOnline)
            {
                StatusMessage =
                    Properties.Resources.FriendList_Msg_Offline;

                return;
            }

            StatusMessage =
                Properties.Resources.FriendList_Msg_InvitationSent;
        }

        private void RemoveFriend(object? parameter)
        {
            if (parameter is not FriendItemViewModel friend)
            {
                return;
            }

            Friends.Remove(friend);

            StatusMessage =
                Properties.Resources.FriendList_Msg_Removed;
        }

        private void GoBack()
        {
            if (string.IsNullOrWhiteSpace(_roomCode))
            {
                _navigationService.NavigateToMainMenu();

                return;
            }

            PrivateRoomNavigationContext navigationContext =
                new PrivateRoomNavigationContext
                {
                    IsHost = true,
                    RoomCode = _roomCode,
                    TargetCaptures = _targetCaptures
                };

            _navigationService.NavigateToPrivateRoomLobby(
                navigationContext);
        }
    }
}