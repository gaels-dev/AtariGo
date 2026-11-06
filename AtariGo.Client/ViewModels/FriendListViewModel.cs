using System;
using System.Collections.ObjectModel;
using System.Windows.Input;

using AtariGo.Client.Commands;
using AtariGo.Client.Services;

namespace AtariGo.Client.ViewModels
{
    public class FriendListViewModel : ViewModelBase
    {
        private const string ExampleFriendOne = "KitsunePlayer";
        private const string ExampleFriendTwo = "GoMaster";
        private const string ExampleFriendThree = "AtariNinja";

        private string _statusMessage;

        public FriendListViewModel(INavigationService navigationService)
        {
            ArgumentNullException.ThrowIfNull(navigationService);

            _statusMessage = string.Empty;

            Friends = new ObservableCollection<FriendItemViewModel>
            {
                new FriendItemViewModel(ExampleFriendOne, true),
                new FriendItemViewModel(ExampleFriendTwo, false),
                new FriendItemViewModel(ExampleFriendThree, true)
            };

            InviteFriendCommand = new RelayCommand(InviteFriend);
            RemoveFriendCommand = new RelayCommand(RemoveFriend);
            BackCommand = new RelayCommand(navigationService.NavigateToMainMenu);
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
    }
}
