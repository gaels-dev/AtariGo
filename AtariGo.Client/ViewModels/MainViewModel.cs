using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

using Grpc.Core;

using AtariGo.Client.Models;
using AtariGo.Client.Services;
using AtariGo.Client.ViewModels.Dialogs;
using AtariGo.Contracts;

namespace AtariGo.Client.ViewModels
{
    public class MainViewModel : ViewModelBase, IDisposable
    {
        private readonly INavigationService _navigationService;
        private readonly ISessionClient? _sessionClient;
        private readonly IGuestIdentityClient? _guestIdentityClient;
        private readonly CancellationTokenSource _lifetimeCancellation =
            new CancellationTokenSource();
        private ViewModelBase _currentViewModel = null!;
        private DialogViewModelBase? _currentDialogViewModel;
        private CancellationTokenSource? _realtimeCancellation;
        private Task? _realtimeTask;
        private bool _isGuest;
        private string _playerName;
        private string _presenceStatusResourceKey = string.Empty;
        private bool _isDisposed;
        private bool _guestCredentialRevoked;

        public MainViewModel(
            ClientSessionContext? sessionContext = null,
            INavigationService? navigationService = null,
            ISessionClient? sessionClient = null)
        {
            sessionContext ??= new ClientSessionContext();
            _isGuest = sessionContext.IsGuest;
            _playerName = sessionContext.PlayerName;
            SessionToken = sessionContext.SessionToken;
            GuestCredential = sessionContext.GuestCredential;
            _navigationService =
                navigationService ?? new NavigationService();
            if (!_isGuest && !string.IsNullOrWhiteSpace(SessionToken))
            {
                _sessionClient = sessionClient
                    ?? new GrpcSessionClient(SessionToken);
            }

            if (_isGuest && !string.IsNullOrWhiteSpace(GuestCredential))
            {
                _guestIdentityClient = new GrpcMatchmakingClient(
                    guestCredential: GuestCredential);
            }

            NavigateToMainMenu();
            StartRealtimeConnection();
        }

        public ViewModelBase CurrentViewModel
        {
            get
            {
                return _currentViewModel;
            }
            set
            {
                ViewModelBase? previousViewModel = _currentViewModel;
                if (SetProperty(ref _currentViewModel, value))
                {
                    (previousViewModel as IDisposable)?.Dispose();
                }
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

        public string? GuestCredential { get; }

        public ObservableCollection<FriendItemViewModel> Friends { get; } =
            new();

        public event Action? LanguageChanged;

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _lifetimeCancellation.Cancel();
            StopRealtimeConnection();
            (CurrentViewModel as IDisposable)?.Dispose();
            _lifetimeCancellation.Dispose();
        }

        public void NavigateToMainMenu()
        {
            if (_isGuest)
            {
                CurrentViewModel = new GuestLobbyViewModel(
                    _navigationService,
                    _playerName,
                    SignOutGuestAsync);
                return;
            }

            CurrentViewModel = new RegisterPlayerLobbyViewModel(
                _navigationService,
                context: new RegisteredLobbyContext
                {
                    Friends = Friends,
                    SessionToken = SessionToken,
                    SignOutAsync = SignOutAsync
                })
            {
                PlayerName = _playerName,
                IsGuest = _isGuest
            };
            SetPresenceStatus(_presenceStatusResourceKey);
        }

        public void StopRealtimeConnection()
        {
            _realtimeCancellation?.Cancel();
        }

        public void NavigateToLobby()
        {
            CurrentViewModel = new SearchingOpponentViewModel(
                _navigationService,
                new GrpcMatchmakingClient(SessionToken, GuestCredential),
                new ClientSessionContext
                {
                    IsGuest = _isGuest,
                    PlayerName = _playerName,
                    SessionToken = SessionToken,
                    GuestCredential = GuestCredential
                });
        }

        public void NavigateToPublicGame(PublicMatchNavigationContext navigationContext)
        {
            GameBoardViewModelContext boardContext = new GameBoardViewModelContext(
                new GrpcGameSessionClient(SessionToken, GuestCredential),
                navigationContext.IsGuest,
                navigationContext.PlayerName);
            GameBoardViewModel gameBoard = new GameBoardViewModel(
                _navigationService,
                boardContext);
            gameBoard.OpenPublicGame(navigationContext);
            CurrentViewModel = gameBoard;
        }

        public async Task RevokeGuestIdentityAsync()
        {
            if (!_isGuest || _guestCredentialRevoked
                || string.IsNullOrWhiteSpace(GuestCredential)
                || _guestIdentityClient is null)
            {
                return;
            }

            _guestCredentialRevoked = true;
            try
            {
                await _guestIdentityClient.RevokeAsync(
                    GuestCredential,
                    CancellationToken.None);
            }
            catch (RpcException exception)
            {
                Trace.TraceWarning(
                    "Guest identity revocation failed with status {0}.",
                    exception.StatusCode);
            }
        }

        private async Task SignOutGuestAsync()
        {
            await RevokeGuestIdentityAsync();
            _navigationService.SignOut();
        }

        public void NavigateToGameBoard(
            bool isGuest = false,
            int targetCaptures = GameRules.DefaultCaptureGoal)
        {
            GameBoardViewModel gameBoard =
                new GameBoardViewModel(
                    _navigationService,
                    new GameBoardViewModelContext(
                        null,
                        isGuest,
                        _playerName))
                {
                    TargetCaptures = targetCaptures
                };

            CurrentViewModel = gameBoard;
        }

        public void NavigateToSharedGame(PrivateRoomNavigationContext navigationContext)
        {
            GameBoardViewModelContext boardContext = new GameBoardViewModelContext(
                new GrpcGameSessionClient(SessionToken, null),
                false,
                _playerName);
            GameBoardViewModel gameBoard = new GameBoardViewModel(
                _navigationService,
                boardContext);
            gameBoard.OpenSharedGame(navigationContext);
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

        private void StartRealtimeConnection()
        {
            if (_isDisposed
                || _sessionClient is null
                || _realtimeTask is { IsCompleted: false })
            {
                return;
            }

            var cancellation = new CancellationTokenSource();
            _realtimeCancellation = cancellation;
            _realtimeTask = RunRealtimeConnectionAsync(cancellation);
        }

        private async Task RunRealtimeConnectionAsync(
            CancellationTokenSource cancellation)
        {
            var callbacks = new SessionRealtimeCallbacks(
                serverEvent => DispatchRealtimeUpdate(
                    () => ApplySessionEvent(serverEvent)),
                exception => DispatchRealtimeUpdate(
                    () => HandleRealtimeFailure(exception)),
                () => DispatchRealtimeUpdate(HandleExpiredSession));

            try
            {
                await _sessionClient!.RunRealtimeAsync(
                    callbacks,
                    cancellation.Token);
            }
            finally
            {
                if (ReferenceEquals(_realtimeCancellation, cancellation))
                {
                    _realtimeCancellation = null;
                }
                cancellation.Dispose();
            }
        }

        private void DispatchRealtimeUpdate(Action update)
        {
            Application? application = Application.Current;
            if (application is null || application.Dispatcher.HasShutdownStarted)
            {
                return;
            }

            application.Dispatcher.Invoke(() =>
            {
                if (_realtimeCancellation is { IsCancellationRequested: false })
                {
                    update();
                }
            });
        }

        private void HandleRealtimeFailure(RpcException exception)
        {
            Trace.TraceWarning(
                "Real-time connection failed. RPC status: {0}.",
                exception.StatusCode);
            SetPresenceStatus(
                GrpcFailureMessages.GetResourceKey(exception.StatusCode));
        }

        private void ApplySessionEvent(SessionServerEvent serverEvent)
        {
            switch (serverEvent.EventCase)
            {
                case SessionServerEvent.EventOneofCase.FriendshipRemoved:
                    RemoveFriendFromList(serverEvent.FriendshipRemoved.FriendUserId);
                    break;
                case SessionServerEvent.EventOneofCase.FriendPresenceSnapshot:
                    ApplyFriendPresenceSnapshot(
                        serverEvent.FriendPresenceSnapshot);
                    break;
                case SessionServerEvent.EventOneofCase.FriendPresenceChanged:
                    ApplyFriendPresenceChange(
                        serverEvent.FriendPresenceChanged.Friend);
                    break;
            }
        }

        private void RemoveFriendFromList(int friendUserId)
        {
            FriendItemViewModel? friend = Friends.FirstOrDefault(
                candidate => candidate.UserId == friendUserId);
            if (friend is not null)
            {
                Friends.Remove(friend);
            }
        }

        private void ApplyFriendPresenceSnapshot(
            FriendPresenceSnapshot snapshot)
        {
            Friends.Clear();
            foreach (FriendPresence friend in snapshot.Friends)
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

        private void ApplyFriendPresenceChange(FriendPresence friendPresence)
        {
            FriendItemViewModel? friend = Friends.FirstOrDefault(
                candidate => candidate.UserId == friendPresence.UserId);
            if (friend is null)
            {
                Friends.Add(new FriendItemViewModel(friendPresence.UserId,
                    friendPresence.UserName, friendPresence.IsOnline));
                return;
            }

            friend.IsOnline = friendPresence.IsOnline;
            SetPresenceStatus(string.Empty);
        }

        private void SetPresenceStatus(string resourceKey)
        {
            _presenceStatusResourceKey = resourceKey;
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
            StopRealtimeConnection();
            _navigationService.SignOutAfterSessionExpired();
        }

        private async Task SignOutAsync()
        {
            if (_isDisposed)
            {
                return;
            }

            if (_sessionClient is null)
            {
                _navigationService.SignOut();
                return;
            }

            CancellationToken cancellationToken = _lifetimeCancellation.Token;
            await StopRealtimeConnectionAsync();
            try
            {
                await _sessionClient.LogoutAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                _navigationService.SignOut();
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                Trace.TraceInformation("Logout wait cancelled because its window was closed.");
            }
            catch (RpcException exception)
                when (exception.StatusCode == StatusCode.Unauthenticated)
            {
                if (!_isDisposed)
                {
                    _navigationService.SignOut();
                }
            }
            catch (RpcException exception)
            {
                HandleLogoutFailure(exception);
                StartRealtimeConnection();
            }
        }

        private void HandleLogoutFailure(RpcException exception)
        {
            Trace.TraceError(
                "Session logout failed. RPC status: {0}.",
                exception.StatusCode);
            HandleRealtimeFailure(exception);
        }

        private async Task StopRealtimeConnectionAsync()
        {
            Task? realtimeTask = _realtimeTask;
            StopRealtimeConnection();
            if (realtimeTask is not null)
            {
                await realtimeTask;
            }

            _realtimeTask = null;
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
