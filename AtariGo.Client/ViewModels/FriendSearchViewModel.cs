using System;
using System.Collections.ObjectModel;
using System.Windows.Input;

using AtariGo.Client.Commands;
using AtariGo.Client.Services;

namespace AtariGo.Client.ViewModels
{
    public class FriendSearchViewModel : ViewModelBase
    {
        private const string ExamplePlayerOne = "KitsunePlayer";
        private const string ExamplePlayerTwo = "GoMaster";
        private const string ExamplePlayerThree = "AtariNinja";

        private readonly INavigationService _navigationService;
        private string _searchText;
        private string _statusMessage;

        public FriendSearchViewModel(
            INavigationService navigationService)
        {
            ArgumentNullException.ThrowIfNull(navigationService);

            _navigationService = navigationService;
            _searchText = string.Empty;
            _statusMessage = string.Empty;

            SearchResults =
                new ObservableCollection<FriendSearchResultViewModel>();

            SearchCommand = new RelayCommand(SearchPlayers);
            AddFriendCommand = new RelayCommand(AddFriend);
            BackCommand = new RelayCommand(
                _navigationService.NavigateToMainMenu);
        }

        public string SearchText
        {
            get
            {
                return _searchText;
            }
            set
            {
                SetProperty(ref _searchText, value);
            }
        }

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

        public ObservableCollection<FriendSearchResultViewModel>
            SearchResults
        { get; }

        public ICommand SearchCommand { get; }

        public ICommand AddFriendCommand { get; }

        public ICommand BackCommand { get; }

        private void SearchPlayers()
        {
            SearchResults.Clear();

            if (string.IsNullOrWhiteSpace(SearchText))
            {
                StatusMessage =
                    Properties.Resources.FriendSearch_Err_EmptySearch;

                return;
            }

            SearchResults.Add(
                new FriendSearchResultViewModel(ExamplePlayerOne));

            SearchResults.Add(
                new FriendSearchResultViewModel(ExamplePlayerTwo));

            SearchResults.Add(
                new FriendSearchResultViewModel(ExamplePlayerThree));

            StatusMessage = string.Empty;
        }

        private void AddFriend(object? parameter)
        {
            if (parameter is not FriendSearchResultViewModel player)
            {
                return;
            }

            player.IsRequestSent = true;

            StatusMessage =
                Properties.Resources.FriendSearch_Msg_RequestSent;
        }
    }
}