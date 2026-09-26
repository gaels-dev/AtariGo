using System;
using System.Windows.Input;
using AtariGo.Client.Commands;
using AtariGo.Client.Services;

namespace AtariGo.Client.ViewModels
{
    public class RegisterPlayerLobbyViewModel : ViewModelBase
    {
        private string _playerName;
        private int _playerWins;
        private bool _isGuest;

        public RegisterPlayerLobbyViewModel(
            INavigationService navigationService)
        {
            ArgumentNullException.ThrowIfNull(navigationService);
            _playerName = "Player";
            _playerWins = 54;
            _isGuest = false;

            PlayMultiplayerCommand = new RelayCommand(navigationService.NavigateToLobby);
            CreatePrivateRoomCommand = new RelayCommand(navigationService.NavigateToLobby);
            JoinWithCodeCommand = new RelayCommand(navigationService.NavigateToLobby);
            OptionsCommand = new RelayCommand(navigationService.OpenOptionsDialog);
            ExitCommand = new RelayCommand(navigationService.ExitApplication);
            SignOutCommand = new RelayCommand(navigationService.SignOut);

            CustomizeProfileCommand = new RelayCommand(() => { });
            AddFriendCommand = new RelayCommand(() => { });
            InviteFriendCommand = new RelayCommand(() => { });
            EmailFriendCommand = new RelayCommand(() => { });
            RemoveFriendCommand = new RelayCommand(() => { });
        }

        public string PlayerName
        {
            get => _playerName;
            set => SetProperty(ref _playerName, value);
        }

        public int PlayerWins
        {
            get => _playerWins;
            set => SetProperty(ref _playerWins, value);
        }

        public bool IsGuest
        {
            get => _isGuest;
            set => SetProperty(ref _isGuest, value);
        }

        public ICommand PlayMultiplayerCommand { get; }

        public ICommand CreatePrivateRoomCommand { get; }

        public ICommand JoinWithCodeCommand { get; }

        public ICommand OptionsCommand { get; }

        public ICommand ExitCommand { get; }

        public ICommand SignOutCommand { get; }

        public ICommand CustomizeProfileCommand { get; }

        public ICommand AddFriendCommand { get; }

        public ICommand InviteFriendCommand { get; }

        public ICommand EmailFriendCommand { get; }

        public ICommand RemoveFriendCommand { get; }
    }
}
