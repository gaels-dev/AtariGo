using System;
using System.Windows.Input;

using AtariGo.Client.Commands;

namespace AtariGo.Client.ViewModels
{
    public class MainMenuViewModel : ViewModelBase
    {
        private string _playerName;
        private bool _isGuest;

        public MainMenuViewModel(Action onNavigateToLobby, Action onExit)
        {
            _playerName = "Player";
            _isGuest = false;

            PlayMultiplayerCommand = new RelayCommand(onNavigateToLobby);
            CreatePrivateRoomCommand = new RelayCommand(onNavigateToLobby);
            JoinWithCodeCommand = new RelayCommand(onNavigateToLobby);
            ExitCommand = new RelayCommand(onExit);
        }

        public string PlayerName
        {
            get => _playerName;
            set => SetProperty(ref _playerName, value);
        }

        public bool IsGuest
        {
            get => _isGuest;
            set => SetProperty(ref _isGuest, value);
        }

        public ICommand PlayMultiplayerCommand { get; }

        public ICommand CreatePrivateRoomCommand { get; }

        public ICommand JoinWithCodeCommand { get; }

        public ICommand ExitCommand { get; }
    }
}
