using System;
using System.Windows.Input;

using AtariGo.Client.Commands;

namespace AtariGo.Client.ViewModels
{
    public class LobbyViewModel : ViewModelBase
    {
        private string _playerName;
        private string _statusMessage;
        private bool _isSearching;

        public LobbyViewModel(Action onStartGame, Action onCancel)
        {
            _playerName = Properties.Resources.Lobby_Lbl_You;
            _statusMessage = Properties.Resources.Lobby_Lbl_Searching;
            _isSearching = true;

            StartGameCommand = new RelayCommand(onStartGame);
            SimulateMatchFoundCommand = new RelayCommand(onStartGame);
            CancelCommand = new RelayCommand(onCancel);
        }

        public string PlayerName
        {
            get => _playerName;
            set => SetProperty(ref _playerName, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public bool IsSearching
        {
            get => _isSearching;
            set => SetProperty(ref _isSearching, value);
        }

        public ICommand StartGameCommand { get; }

        public ICommand SimulateMatchFoundCommand { get; }

        public ICommand CancelCommand { get; }
    }
}
