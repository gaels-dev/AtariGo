using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Input;

using AtariGo.Client.Commands;
using AtariGo.Client.Properties;
using AtariGo.Client.Services;
using AtariGo.Contracts;

namespace AtariGo.Client.ViewModels
{
    public class RegisteredPlayerLobbyViewModel : ViewModelBase, IDisposable
    {
        private const int LeaderboardLimit = 10;
        private const string DefaultPlayerName = "Player";

        private readonly ILeaderboardClient _leaderboardClient;
        private readonly IProfileClient? _profileClient;
        private readonly Action<int>? _avatarLoaded;
        private readonly CancellationTokenSource _lifetimeCancellation =
            new CancellationTokenSource();

        private string _playerName;
        private int _playerWins;
        private int? _playerAvatarId;
        private bool _isLeaderboardLoading;
        private string _leaderboardStatus = string.Empty;
        private string _presenceStatus = string.Empty;
        private string _friendSearchText = string.Empty;

        public RegisteredPlayerLobbyViewModel(
            INavigationService navigationService,
            ILeaderboardClient? leaderboardClient = null,
            RegisteredLobbyContext? context = null)
        {
            ArgumentNullException.ThrowIfNull(navigationService);

            _leaderboardClient =
                leaderboardClient ?? new GrpcLeaderboardClient();
            _profileClient = context?.ProfileClient;
            _avatarLoaded = context?.AvatarLoaded;
            _playerName = DefaultPlayerName;
            _playerAvatarId = context?.InitialAvatarId;
            RoomInvitations = context?.RoomInvitations;
            Friends = context?.Friends ?? new ObservableCollection<FriendItemViewModel>();
            FriendRemoval = new FriendRemovalViewModel(navigationService,
                new FriendListNavigationContext
                {
                    SessionToken = context?.SessionToken,
                    Friends = Friends
                });
            FilteredFriends = CollectionViewSource.GetDefaultView(Friends);
            FilteredFriends.Filter = IsFriendVisible;
            Friends.CollectionChanged += HandleFriendsChanged;

            InitializePlayCommands(navigationService);
            InitializeAccountCommands(navigationService, context?.SignOutAsync);

            LeaderboardStatus = GetLocalizedMessage(
                "Leaderboard_Msg_Loading");

            _ = LoadLeaderboardAsync();
            if (_profileClient is not null)
            {
                _ = LoadPlayerProfileAsync();
            }
        }

        public ObservableCollection<LeaderboardEntry>
            LeaderboardEntries
        {
            get;
        } = new();

        public ObservableCollection<FriendItemViewModel> Friends
        {
            get;
        }

        public RoomInvitationsViewModel? RoomInvitations
        {
            get;
        }

        public ICollectionView FilteredFriends
        {
            get;
        }

        public FriendRemovalViewModel FriendRemoval
        {
            get;
        }

        public string FriendSearchText
        {
            get => _friendSearchText;
            set
            {
                if (SetProperty(ref _friendSearchText, value))
                {
                    FilteredFriends.Refresh();
                    OnPropertyChanged(nameof(FriendSearchStatus));
                }
            }
        }

        public string FriendSearchStatus =>
            !string.IsNullOrWhiteSpace(FriendSearchText)
                && FilteredFriends.IsEmpty
                    ? GetLocalizedMessage(
                        "FriendList_Msg_NoMatches")
                    : string.Empty;

        public string PresenceStatus
        {
            get => _presenceStatus;
            set => SetProperty(ref _presenceStatus, value);
        }

        public string RankHeader =>
            GetLocalizedMessage("Leaderboard_Col_Rank");

        public string PlayerHeader =>
            GetLocalizedMessage("Leaderboard_Col_Player");

        public string WinsHeader =>
            GetLocalizedMessage(
                "RegisterPlayerLobby_Lbl_WinsHeader");

        public string RefreshLabel =>
            GetLocalizedMessage("Leaderboard_Btn_Refresh");

        public bool IsLeaderboardLoading
        {
            get
            {
                return _isLeaderboardLoading;
            }
            private set
            {
                if (SetProperty(
                    ref _isLeaderboardLoading,
                    value))
                {
                    OnPropertyChanged(
                        nameof(IsRefreshEnabled));
                }
            }
        }

        public bool IsRefreshEnabled =>
            !IsLeaderboardLoading && !_lifetimeCancellation.IsCancellationRequested;

        public string LeaderboardStatus
        {
            get
            {
                return _leaderboardStatus;
            }
            private set
            {
                SetProperty(
                    ref _leaderboardStatus,
                    value);
            }
        }

        public string PlayerName
        {
            get
            {
                return _playerName;
            }
            set
            {
                SetProperty(ref _playerName, value);
            }
        }

        public int PlayerWins
        {
            get
            {
                return _playerWins;
            }
            set
            {
                SetProperty(ref _playerWins, value);
            }
        }

        public int? PlayerAvatarId
        {
            get => _playerAvatarId;
            private set
            {
                if (SetProperty(ref _playerAvatarId, value))
                {
                    OnPropertyChanged(nameof(HasPlayerAvatar));
                }
            }
        }

        public bool HasPlayerAvatar => PlayerAvatarId.HasValue;

        public ICommand PlayMultiplayerCommand { get; private set; } = null!;

        public ICommand CreatePrivateRoomCommand { get; private set; } = null!;

        public ICommand JoinWithCodeCommand { get; private set; } = null!;

        public ICommand OptionsCommand { get; private set; } = null!;

        public ICommand ExitCommand { get; private set; } = null!;

        public ICommand SignOutCommand { get; private set; } = null!;

        public ICommand CustomizeProfileCommand { get; private set; } = null!;

        public ICommand AddFriendCommand { get; private set; } = null!;

        public ICommand InviteFriendCommand { get; private set; } = null!;

        public ICommand InviteFriendByEmailCommand { get; private set; } = null!;

        public ICommand ManageFriendRequestsCommand { get; private set; } = null!;

        public string ManageFriendRequestsLabel => GetLocalizedMessage(
            "FriendRequests_Btn_Manage");


        public ICommand RemoveFriendCommand { get; private set; } = null!;

        public ICommand RefreshLeaderboardCommand { get; private set; } = null!;

        public ICommand GameHistoryCommand { get; private set; } = null!;

        public string GameHistoryLabel => GetLocalizedMessage("GameHistory_Lbl_Title");

        public void Dispose()
        {
            FriendRemoval.Dispose();
            Friends.CollectionChanged -= HandleFriendsChanged;
            if (FilteredFriends.Filter == IsFriendVisible)
            {
                FilteredFriends.Filter = null;
            }
            if (!_lifetimeCancellation.IsCancellationRequested)
            {
                _lifetimeCancellation.Cancel();
                _lifetimeCancellation.Dispose();
            }
        }

        private void InitializePlayCommands(INavigationService navigationService)
        {
            PlayMultiplayerCommand = new RelayCommand(navigationService.NavigateToLobby);
            CreatePrivateRoomCommand = new RelayCommand(
                navigationService.NavigateToCreatePrivateRoom);
            JoinWithCodeCommand = new RelayCommand(navigationService.NavigateToJoinPrivateRoom);
            InviteFriendCommand = new RelayCommand(parameter =>
                OpenInvitation(navigationService, parameter, isOnline: true));
            InviteFriendByEmailCommand = new RelayCommand(parameter =>
                OpenInvitation(navigationService, parameter, isOnline: false));
            RemoveFriendCommand = FriendRemoval.RemoveCommand;
        }

        private void OpenInvitation(INavigationService navigation, object? parameter, bool isOnline)
        {
            if (parameter is FriendItemViewModel friend && friend.IsOnline == isOnline
                && Friends.Contains(friend) && friend.UserId > 0)
            {
                navigation.NavigateToCreatePrivateRoomForInvitation(friend.UserId);
            }
        }

        private void InitializeAccountCommands(
            INavigationService navigationService,
            Func<Task>? signOutAsync)
        {
            OptionsCommand = new RelayCommand(navigationService.OpenOptionsDialog);
            GameHistoryCommand = new RelayCommand(navigationService.NavigateToGameHistory);
            ExitCommand = new RelayCommand(navigationService.ExitApplication);
            SignOutCommand = signOutAsync is null
                ? new RelayCommand(navigationService.SignOut)
                : new AsyncRelayCommand(_ => signOutAsync(), HandleSessionFailure);
            CustomizeProfileCommand = new RelayCommand(
                navigationService.NavigateToProfileCustomization);
            AddFriendCommand = new RelayCommand(navigationService.NavigateToFriendSearch);
            ManageFriendRequestsCommand = new RelayCommand(
                navigationService.NavigateToFriendRequests);
            RefreshLeaderboardCommand = new AsyncRelayCommand(
                _ => LoadLeaderboardAsync(), HandleLeaderboardFailure);
        }

        private void HandleFriendsChanged(object? sender, NotifyCollectionChangedEventArgs args)
        {
            OnPropertyChanged(nameof(FriendSearchStatus));
        }

        private bool IsFriendVisible(object item)
        {
            if (item is not FriendItemViewModel friend)
            {
                return false;
            }

            string query = FriendSearchText.Trim();
            return string.IsNullOrEmpty(query)
                || friend.UserName.Contains(
                    query,
                    StringComparison.OrdinalIgnoreCase);
        }

        private async Task LoadLeaderboardAsync()
        {
            if (IsLeaderboardLoading || _lifetimeCancellation.IsCancellationRequested)
            {
                return;
            }

            IsLeaderboardLoading = true;
            LeaderboardStatus = GetLocalizedMessage(
                "Leaderboard_Msg_Loading");
            CancellationToken cancellationToken = _lifetimeCancellation.Token;

            try
            {
                GetTopPlayersResponse response =
                    await _leaderboardClient.GetTopPlayersAsync(
                        LeaderboardLimit,
                        cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                ApplyLeaderboardResponse(response);
            }
            catch (Exception exception)
                when (GrpcCallPolicy.IsRequestedCancellation(exception, cancellationToken))
            {
                Trace.TraceInformation("Leaderboard refresh cancelled because its view was left.");
            }
            catch (Exception exception) when (GrpcClientFailures.IsTransportFailure(exception))
            {
                LeaderboardEntries.Clear();
                LeaderboardStatus = ClientFailureMessages.GetMessage(exception);
            }
            finally
            {
                IsLeaderboardLoading = false;
            }
        }

        private async Task LoadPlayerProfileAsync()
        {
            CancellationToken cancellationToken = _lifetimeCancellation.Token;
            try
            {
                UserProfile profile = await _profileClient!.GetProfileAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                PlayerWins = profile.Wins;
                PlayerAvatarId = profile.AvatarId;
                _avatarLoaded?.Invoke(profile.AvatarId);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Trace.TraceInformation("Lobby profile load was cancelled.");
            }
            catch (Exception exception) when (GrpcClientFailures.IsTransportFailure(exception))
            {
                Trace.TraceWarning("Lobby profile load failed with status {0}.",
                    GrpcClientFailures.GetReason(exception));
            }
        }

        private void ApplyLeaderboardResponse(GetTopPlayersResponse response)
        {
            LeaderboardEntries.Clear();
            foreach (LeaderboardEntry entry in response.Entries)
            {
                LeaderboardEntries.Add(entry);
            }

            LeaderboardStatus = LeaderboardEntries.Count == 0
                ? GetLocalizedMessage("Leaderboard_Msg_Empty")
                : string.Empty;
        }

        private void HandleLeaderboardFailure(Exception exception)
        {
            LeaderboardStatus = ClientFailureMessages.GetMessage(exception);
        }

        private void HandleSessionFailure(Exception exception)
        {
            PresenceStatus = ClientFailureMessages.GetMessage(exception);
        }

        private static string GetLocalizedMessage(
            string key) =>
            Resources.ResourceManager.GetString(
                key,
                CultureInfo.CurrentUICulture)
            ?? key;
    }
}
