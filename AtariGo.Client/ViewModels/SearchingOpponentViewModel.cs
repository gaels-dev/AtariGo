using System;
using System.Windows.Input;

using AtariGo.Client.Commands;
using AtariGo.Client.Services;

namespace AtariGo.Client.ViewModels
{
    public class SearchingOpponentViewModel : ViewModelBase
    {
        private string _playerName;
        private string _statusMessage;
        private bool _isSearching;

        public SearchingOpponentViewModel(INavigationService navigationService, bool isGuest)
        {
            ArgumentNullException.ThrowIfNull(navigationService);
            _playerName = Properties.Resources.SearchingOpponent_Lbl_You;
            _statusMessage = Properties.Resources.SearchingOpponent_Lbl_Searching;
            _isSearching = true;

            StartGameCommand = new RelayCommand(
                () => navigationService.NavigateToGameBoard(isGuest, 1));
            SimulateMatchFoundCommand = new RelayCommand(
                () => navigationService.NavigateToGameBoard(isGuest, 1));
            CancelCommand = new RelayCommand(navigationService.NavigateToMainMenu);
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
