using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Input;
using AtariGo.Client.Commands;
using AtariGo.Client.Properties;
using AtariGo.Client.Services;
using AtariGo.Contracts;
using Grpc.Core;

namespace AtariGo.Client.ViewModels
{
    public class RegisterPlayerLobbyViewModel : ViewModelBase
    {
        private string _playerName;
        private int _playerWins;
        private bool _isGuest;
        private bool _isLeaderboardLoading;
        private string _leaderboardStatus = string.Empty;
        private readonly ILeaderboardClient _leaderboardClient;

        private const int LeaderboardLimit = 10;

        public RegisterPlayerLobbyViewModel(
            INavigationService navigationService,
            ILeaderboardClient? leaderboardClient = null)
        {
            ArgumentNullException.ThrowIfNull(navigationService);
            _leaderboardClient = leaderboardClient ?? new GrpcLeaderboardClient();
            _playerName = "Player";
            _playerWins = 54;
            _isGuest = false;

            PlayMultiplayerCommand = new RelayCommand(navigationService.NavigateToLobby);
            CreatePrivateRoomCommand = new RelayCommand(navigationService.NavigateToLobby);
            JoinWithCodeCommand = new RelayCommand(navigationService.NavigateToLobby);
            OptionsCommand = new RelayCommand(navigationService.OpenOptionsDialog);
            ExitCommand = new RelayCommand(navigationService.ExitApplication);
            SignOutCommand = new RelayCommand(navigationService.SignOut);
            RefreshLeaderboardCommand = new AsyncRelayCommand(
                _ => LoadLeaderboardAsync());

            CustomizeProfileCommand = new RelayCommand(() => { });
            AddFriendCommand = new RelayCommand(() => { });
            InviteFriendCommand = new RelayCommand(() => { });
            EmailFriendCommand = new RelayCommand(() => { });
            RemoveFriendCommand = new RelayCommand(() => { });

            LeaderboardStatus = GetLocalizedMessage("Leaderboard_Msg_Loading");
            _ = LoadLeaderboardAsync();
        }

        public ObservableCollection<LeaderboardEntry> LeaderboardEntries { get; } = new();

        public string RankHeader => GetLocalizedMessage("Leaderboard_Col_Rank");

        public string PlayerHeader => GetLocalizedMessage("Leaderboard_Col_Player");

        public string WinsHeader => GetLocalizedMessage("RegisterPlayerLobby_Lbl_WinsHeader");

        public string RefreshLabel => GetLocalizedMessage("Leaderboard_Btn_Refresh");

        public bool IsLeaderboardLoading
        {
            get => _isLeaderboardLoading;
            private set
            {
                if (SetProperty(ref _isLeaderboardLoading, value))
                {
                    OnPropertyChanged(nameof(IsRefreshEnabled));
                }
            }
        }

        public bool IsRefreshEnabled => !IsLeaderboardLoading;

        public string LeaderboardStatus
        {
            get => _leaderboardStatus;
            private set => SetProperty(ref _leaderboardStatus, value);
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

        public ICommand RefreshLeaderboardCommand { get; }

        private async Task LoadLeaderboardAsync()
        {
            if (IsLeaderboardLoading)
            {
                return;
            }

            IsLeaderboardLoading = true;
            LeaderboardStatus = GetLocalizedMessage("Leaderboard_Msg_Loading");

            try
            {
                GetTopPlayersResponse response = await _leaderboardClient
                    .GetTopPlayersAsync(LeaderboardLimit);

                LeaderboardEntries.Clear();
                foreach (LeaderboardEntry entry in response.Entries)
                {
                    LeaderboardEntries.Add(entry);
                }

                LeaderboardStatus = LeaderboardEntries.Count == 0
                    ? GetLocalizedMessage("Leaderboard_Msg_Empty")
                    : string.Empty;
            }
            catch (RpcException)
            {
                LeaderboardStatus = GetLocalizedMessage("Leaderboard_Msg_Error");
            }
            catch (Exception)
            {
                LeaderboardStatus = GetLocalizedMessage("Leaderboard_Msg_Error");
            }
            finally
            {
                IsLeaderboardLoading = false;
            }
        }

        private static string GetLocalizedMessage(string key) =>
            Resources.ResourceManager.GetString(key, CultureInfo.CurrentUICulture)
            ?? key;
    }
}
