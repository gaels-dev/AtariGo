using System;
using System.Collections.Generic;
using System.Windows.Input;

using AtariGo.Client.Commands;

namespace AtariGo.Client.ViewModels
{
    public class LobbyViewModel : ViewModelBase
    {
        private string _roomCode;
        private string _statusMessage;
        private int _selectedCaptureTarget;
        private bool _isHost;
        private bool _isSearching;

        public LobbyViewModel(Action onStartGame, Action onCancel)
        {
            _roomCode = "AG-" + new Random().Next(1000, 9999);
            _statusMessage = "Waiting for opponent...";
            _selectedCaptureTarget = 1;
            _isHost = true;
            _isSearching = true;

            CaptureTargetOptions = new List<int> { 1, 3, 5 };
            StartGameCommand = new RelayCommand(onStartGame);
            CancelCommand = new RelayCommand(onCancel);
        }

        public string RoomCode
        {
            get => _roomCode;
            set => SetProperty(ref _roomCode, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public IReadOnlyList<int> CaptureTargetOptions { get; }

        public int SelectedCaptureTarget
        {
            get => _selectedCaptureTarget;
            set => SetProperty(ref _selectedCaptureTarget, value);
        }

        public bool IsHost
        {
            get => _isHost;
            set => SetProperty(ref _isHost, value);
        }

        public bool IsSearching
        {
            get => _isSearching;
            set => SetProperty(ref _isSearching, value);
        }

        public ICommand StartGameCommand { get; }

        public ICommand CancelCommand { get; }
    }
}
