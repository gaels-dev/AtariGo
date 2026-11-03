using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

using Grpc.Core;

using AtariGo.Client.Commands;
using AtariGo.Client.Properties;
using AtariGo.Client.Services;
using AtariGo.Contracts;

namespace AtariGo.Client.ViewModels
{
    public class FriendSearchViewModel : ViewModelBase, IDisposable
    {
        private const int MinimumSearchLength = 2;
        private const int MaximumSearchLength = 15;

        private readonly CancellationTokenSource _lifetimeCancellation = new();
        private readonly CancellationToken _lifetimeToken;
        private bool _isDisposed;
        private readonly INavigationService _navigationService;
        private readonly IFriendClient? _friendClient;
        private string _searchText;
        private string _statusMessage;

        public FriendSearchViewModel(
            INavigationService navigationService,
            string? sessionToken,
            IFriendClient? friendClient = null)
        {
            ArgumentNullException.ThrowIfNull(navigationService);

            _lifetimeToken = _lifetimeCancellation.Token;
            _navigationService = navigationService;
            _friendClient = friendClient
                ?? (string.IsNullOrWhiteSpace(sessionToken)
                    ? null
                    : new GrpcFriendClient(sessionToken));
            _searchText = string.Empty;
            _statusMessage = string.Empty;

            SearchResults =
                new ObservableCollection<FriendSearchResultViewModel>();

            SearchCommand = new AsyncRelayCommand(
                _ => SearchPlayersAsync(),
                HandleServiceFailure,
                _lifetimeToken);
            AddFriendCommand = new AsyncRelayCommand(
                AddFriendAsync,
                HandleServiceFailure,
                _lifetimeToken);
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

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _lifetimeCancellation.Cancel();
            _lifetimeCancellation.Dispose();
        }

        private async Task SearchPlayersAsync()
        {
            if (_isDisposed)
            {
                return;
            }

            SearchResults.Clear();

            string query = SearchText.Trim();
            if (query.Length < MinimumSearchLength)
            {
                StatusMessage = GetLocalizedMessage(
                    "FriendSearch_Err_MinLength");
                return;
            }

            if (query.Length > MaximumSearchLength)
            {
                StatusMessage = GetLocalizedMessage(
                    "FriendSearch_Err_InvalidLength");
                return;
            }

            if (_friendClient is null)
            {
                HandleServiceFailure(
                    new InvalidOperationException(
                        "The authenticated friend service is unavailable."));
                return;
            }

            StatusMessage = GetLocalizedMessage(
                "FriendSearch_Msg_Searching");
            SearchUsersResponse response =
                await _friendClient.SearchUsersAsync(query, _lifetimeToken);

            _lifetimeToken.ThrowIfCancellationRequested();

            foreach (FriendAccount account in response.Users)
            {
                SearchResults.Add(
                    new FriendSearchResultViewModel(
                        account.UserId,
                        account.UserName));
            }

            StatusMessage = SearchResults.Count == 0
                ? GetLocalizedMessage("FriendSearch_Msg_NoResults")
                : string.Empty;
        }

        private async Task AddFriendAsync(object? parameter)
        {
            if (_isDisposed || parameter is not FriendSearchResultViewModel player)
            {
                return;
            }

            if (_friendClient is null)
            {
                HandleServiceFailure(
                    new InvalidOperationException(
                        "The authenticated friend service is unavailable."));
                return;
            }

            SendFriendRequestResponse response =
                await _friendClient.SendFriendRequestAsync(player.UserId, _lifetimeToken);

            _lifetimeToken.ThrowIfCancellationRequested();

            StatusMessage = response.Status switch
            {
                FriendRequestStatus.Sent =>
                    GetLocalizedMessage("FriendSearch_Msg_RequestSent"),
                FriendRequestStatus.AlreadyFriends =>
                    GetLocalizedMessage("FriendSearch_Msg_AlreadyFriends"),
                FriendRequestStatus.AlreadyPending =>
                    GetLocalizedMessage("FriendSearch_Msg_AlreadyPending"),
                FriendRequestStatus.UserNotFound =>
                    GetLocalizedMessage("FriendSearch_Msg_UserNotFound"),
                FriendRequestStatus.CannotRequestSelf =>
                    GetLocalizedMessage("FriendSearch_Err_CannotRequestSelf"),
                _ => GetLocalizedMessage("FriendSearch_Err_Service")
            };

            if (response.Status == FriendRequestStatus.Sent
                || response.Status
                    == FriendRequestStatus.AlreadyPending)
            {
                player.MarkRequestPending();
            }
            else if (response.Status
                == FriendRequestStatus.AlreadyFriends)
            {
                player.MarkAlreadyFriend();
            }
        }

        private void HandleServiceFailure(Exception exception)
        {
            if (_isDisposed || GrpcCallPolicy.IsRequestedCancellation(
                    exception, _lifetimeToken))
            {
                return;
            }

            _ = GrpcFailureMessages.GetMessage(exception);

            StatusMessage = exception is RpcException rpcException
                && rpcException.StatusCode == StatusCode.Unauthenticated
                    ? GetLocalizedMessage("FriendSearch_Err_SessionExpired")
                    : GetLocalizedMessage("FriendSearch_Err_Service");
        }

        private static string GetLocalizedMessage(string key)
        {
            return Resources.ResourceManager.GetString(
                key,
                CultureInfo.CurrentUICulture)
                ?? key;
        }
    }
}
