using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

using AtariGo.Client.Services;
using AtariGo.Client.Models;
using AtariGo.Contracts;
using AtariGo.Client.ViewModels.Dialogs;
using Grpc.Core;
using System.Windows;

namespace AtariGo.Client.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private static readonly TimeSpan PresenceHeartbeatInterval =
            TimeSpan.FromSeconds(30);
        private readonly INavigationService _navigationService;
        private readonly ISessionClient? _sessionClient;
        private ViewModelBase _currentViewModel = null!;
        private DialogViewModelBase? _currentDialogViewModel;
        private CancellationTokenSource? _presenceCancellation;
        private Task? _presenceTask;
        private bool _isGuest;
        private string _playerName;

        public MainViewModel(
            bool isGuest = false,
            string playerName = "Player",
            INavigationService? navigationService = null,
            string? sessionToken = null,
            ISessionClient? sessionClient = null)
        {
            _isGuest = isGuest;
            _playerName = playerName;
            SessionToken = sessionToken;
            _navigationService =
                navigationService ?? new NavigationService();
            if (!_isGuest && !string.IsNullOrWhiteSpace(sessionToken))
            {
                _sessionClient = sessionClient
                    ?? new GrpcSessionClient(sessionToken);
            }

            NavigateToMainMenu();
            StartPresenceMonitor();
        }

        public ViewModelBase CurrentViewModel
        {
            get
            {
                return _currentViewModel;
            }
            set
            {
                SetProperty(ref _currentViewModel, value);
            }
        }

        public DialogViewModelBase? CurrentDialogViewModel
        {
            get
            {
                return _currentDialogViewModel;
            }
            private set
            {
                if (SetProperty(ref _currentDialogViewModel, value))
                {
                    OnPropertyChanged(nameof(IsDialogVisible));
                }
            }
        }

        public bool IsDialogVisible
        {
            get
            {
                return _currentDialogViewModel != null;
            }
        }

        public bool IsGuest
        {
            get
            {
                return _isGuest;
            }
            set
            {
                SetProperty(ref _isGuest, value);
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

        public string? SessionToken { get; }

        public ObservableCollection<FriendItemViewModel> Friends { get; } =
            new();

        public event Action? LanguageChanged;

        public void NavigateToMainMenu()
        {
            if (_isGuest)
            {
                CurrentViewModel = new GuestLobbyViewModel(
                    _navigationService,
                    _playerName);
                return;
            }

            CurrentViewModel = new RegisterPlayerLobbyViewModel(
                _navigationService,
                friends: Friends,
                signOutAsync: SignOutAsync)
            {
                PlayerName = _playerName,
                IsGuest = _isGuest
            };
        }

        public void StopPresenceMonitor()
        {
            _presenceCancellation?.Cancel();
        }

        private void StartPresenceMonitor()
        {
            if (_sessionClient is null || _presenceTask is not null)
            {
                return;
            }

            _presenceCancellation = new CancellationTokenSource();
            _presenceTask = RunPresenceMonitorAsync(
                _presenceCancellation.Token);
        }

        private async Task RunPresenceMonitorAsync(
            CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    HeartbeatResponse response =
                        await _sessionClient!.HeartbeatAsync(
                            cancellationToken);
                    await Application.Current.Dispatcher.InvokeAsync(
                        () => ApplyFriendPresence(response));
                }
                catch (RpcException exception)
                    when (exception.StatusCode == StatusCode.Unauthenticated)
                {
                    await Application.Current.Dispatcher.InvokeAsync(
                        HandleExpiredSession);
                    return;
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    Trace.TraceError(
                        "Presence heartbeat failed: {0}",
                        exception.GetType().Name);
                    await Application.Current.Dispatcher.InvokeAsync(
                        () => SetPresenceStatus(
                            "Presence_Msg_Unavailable"));
                }

                try
                {
                    await Task.Delay(
                        PresenceHeartbeatInterval,
                        cancellationToken);
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
            }
        }

        private void ApplyFriendPresence(HeartbeatResponse response)
        {
            Friends.Clear();
            foreach (AtariGo.Contracts.FriendPresence friend
                     in response.Friends)
            {
                Friends.Add(new FriendItemViewModel(
                    friend.UserId,
                    friend.UserName,
                    friend.IsOnline));
            }

            SetPresenceStatus(
                Friends.Count == 0
                    ? "Presence_Msg_Empty"
                    : string.Empty);
        }

        private void SetPresenceStatus(string resourceKey)
        {
            if (CurrentViewModel is RegisterPlayerLobbyViewModel lobby)
            {
                lobby.PresenceStatus = string.IsNullOrEmpty(resourceKey)
                    ? string.Empty
                    : Properties.Resources.ResourceManager.GetString(
                        resourceKey,
                        CultureInfo.CurrentUICulture)
                        ?? resourceKey;
            }
        }

        private void HandleExpiredSession()
        {
            StopPresenceMonitor();
            _navigationService.SignOut();
        }

        private async Task SignOutAsync()
        {
            if (_sessionClient is null)
            {
                _navigationService.SignOut();
                return;
            }

            await StopPresenceMonitorAsync();
            try
            {
                await _sessionClient.LogoutAsync();
                _navigationService.SignOut();
            }
            catch (RpcException exception)
                when (exception.StatusCode == StatusCode.Unauthenticated)
            {
                _navigationService.SignOut();
            }
            catch (Exception exception)
            {
                Trace.TraceError(
                    "Session logout failed: {0}",
                    exception.GetType().Name);
                SetPresenceStatus("Session_Msg_LogoutFailed");
                StartPresenceMonitor();
            }
        }

        private async Task StopPresenceMonitorAsync()
        {
            CancellationTokenSource? cancellation = _presenceCancellation;
            Task? monitorTask = _presenceTask;
            if (cancellation is null)
            {
                return;
            }

            cancellation.Cancel();
            if (monitorTask is not null)
            {
                await monitorTask;
            }

            cancellation.Dispose();
            _presenceCancellation = null;
            _presenceTask = null;
        }

        public void NavigateToLobby()
        {
            CurrentViewModel = new SearchingOpponentViewModel(
                _navigationService,
                _isGuest,
                _playerName);
        }

        public void NavigateToGameBoard(
            bool isGuest = false,
            int targetCaptures = GameRules.DefaultCaptureGoal)
        {
            GameBoardViewModel gameBoard =
                new GameBoardViewModel(
                    _navigationService,
                    isGuest: isGuest,
                    playerName: _playerName)
                {
                    TargetCaptures = targetCaptures
                };

            CurrentViewModel = gameBoard;
        }

        public void OpenOptionsDialog()
        {
            OptionsDialogViewModel dialog =
                new OptionsDialogViewModel();

            dialog.DialogClosed += result =>
            {
                if (result == true)
                {
                    ApplyLanguage(dialog.SelectedCultureCode);
                }
            };

            OpenDialog(dialog);
        }

        public void OpenDialog(DialogViewModelBase dialog)
        {
            dialog.DialogClosed += _ => CloseDialog();
            CurrentDialogViewModel = dialog;
        }

        public void CloseDialog()
        {
            CurrentDialogViewModel = null;
        }

        private void ApplyLanguage(string cultureCode)
        {
            CultureInfo culture = new CultureInfo(cultureCode);

            Properties.Resources.Culture = culture;
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;

            CloseDialog();
            LanguageChanged?.Invoke();
        }
    }
}
