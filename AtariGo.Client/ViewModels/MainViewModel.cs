using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
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
        private static readonly TimeSpan s_presenceHeartbeatInterval =
            TimeSpan.FromSeconds(15);
        private readonly INavigationService _navigationService;
        private readonly ISessionClient? _sessionClient;
        private readonly CancellationTokenSource _lifetimeCancellation =
            new CancellationTokenSource();
        private ViewModelBase _currentViewModel = null!;
        private DialogViewModelBase? _currentDialogViewModel;
        private CancellationTokenSource? _presenceCancellation;
        private Task? _presenceTask;
        private bool _isGuest;
        private string _playerName;
        private string _presenceStatusResourceKey = string.Empty;
        private bool _isDisposed;

        public MainViewModel(
            ClientSessionContext? sessionContext = null,
            INavigationService? navigationService = null,
            ISessionClient? sessionClient = null)
        {
            sessionContext ??= new ClientSessionContext();
            _isGuest = sessionContext.IsGuest;
            _playerName = sessionContext.PlayerName;
            SessionToken = sessionContext.SessionToken;
            _navigationService =
                navigationService ?? new NavigationService();
            if (!_isGuest && !string.IsNullOrWhiteSpace(SessionToken))
            {
                _sessionClient = sessionClient
                    ?? new GrpcSessionClient(SessionToken);
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
            StopPresenceMonitor();
            (CurrentViewModel as IDisposable)?.Dispose();
            _lifetimeCancellation.Dispose();
        }

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
                context: new RegisteredLobbyContext
                {
                    Friends = Friends,
                    SignOutAsync = SignOutAsync
                })
            {
                PlayerName = _playerName,
                IsGuest = _isGuest
            };
            SetPresenceStatus(_presenceStatusResourceKey);
        }

        public void StopPresenceMonitor()
        {
            _presenceCancellation?.Cancel();
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

        private void StartPresenceMonitor()
        {
            if (_isDisposed || _sessionClient is null || _presenceTask is { IsCompleted: false })
            {
                return;
            }

            var cancellation = new CancellationTokenSource();
            _presenceCancellation = cancellation;
            _presenceTask = RunPresenceMonitorAsync(cancellation);
        }

        private async Task RunPresenceMonitorAsync(
            CancellationTokenSource cancellation)
        {
            var monitor = new SessionPresenceMonitor(_sessionClient!, s_presenceHeartbeatInterval);
            monitor.PresenceReceived += response =>
                DispatchPresenceUpdate(() => ApplyFriendPresence(response));
            monitor.RequestFailed += exception =>
                DispatchPresenceUpdate(() => HandlePresenceFailure(exception));
            monitor.SessionExpired += () => DispatchPresenceUpdate(HandleExpiredSession);

            try
            {
                await monitor.RunAsync(cancellation.Token);
            }
            catch (Exception exception)
            {
                Trace.TraceError("Presence monitoring stopped. Exception type: {0}.",
                    exception.GetType().Name);
                DispatchPresenceUpdate(() => HandlePresenceFailure(exception));
            }
            finally
            {
                if (ReferenceEquals(_presenceCancellation, cancellation))
                {
                    _presenceCancellation = null;
                }
                cancellation.Dispose();
            }
        }

        private void DispatchPresenceUpdate(Action update)
        {
            Application? application = Application.Current;
            if (application is null || application.Dispatcher.HasShutdownStarted)
            {
                return;
            }

            application.Dispatcher.Invoke(() =>
            {
                if (_presenceCancellation is { IsCancellationRequested: false })
                {
                    update();
                }
            });
        }

        private void HandlePresenceFailure(Exception exception)
        {
            Friends.Clear();
            string resourceKey = exception is RpcException rpcException
                ? GrpcFailureMessages.GetResourceKey(rpcException.StatusCode)
                : "Connection_Err_RequestFailed";
            SetPresenceStatus(resourceKey);
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
            StopPresenceMonitor();
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
            await StopPresenceMonitorAsync();
            try
            {
                await _sessionClient.LogoutAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                _navigationService.SignOut();
            }
            catch (Exception exception)
                when (GrpcCallPolicy.IsRequestedCancellation(exception, cancellationToken))
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
            catch (Exception exception)
            {
                Trace.TraceError(
                    "Session logout failed: {0}",
                    exception.GetType().Name);
                HandlePresenceFailure(exception);
                StartPresenceMonitor();
            }
        }

        private async Task StopPresenceMonitorAsync()
        {
            Task? monitorTask = _presenceTask;
            StopPresenceMonitor();
            if (monitorTask is not null)
            {
                await monitorTask;
            }

            _presenceTask = null;
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
