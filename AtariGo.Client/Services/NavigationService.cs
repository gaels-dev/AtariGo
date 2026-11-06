using System;
using System.Linq;
using System.Windows;
using System.Windows.Threading;

using AtariGo.Client.Models;
using AtariGo.Client.ViewModels;
using AtariGo.Client.ViewModels.Dialogs;
using AtariGo.Client.Views;
using AtariGo.Contracts;

namespace AtariGo.Client.Services
{
    /// <summary>
    /// Routes navigation requests through the main view model and marshals
    /// requests from background services onto the WPF dispatcher.
    /// </summary>
    public sealed class NavigationService : INavigationService
    {
        public void NavigateToMainWindow(
            bool isGuest,
            string playerName,
            string? sessionToken = null,
            string? guestCredential = null)
        {
            Dispatch(() => ShowMainWindow(
                isGuest, playerName, sessionToken, guestCredential));
        }

        public void RefreshLoginView()
        {
            Dispatch(ReplaceLoginWindow);
        }

        public void NavigateToMainMenu()
        {
            Dispatch(() => GetMainViewModel()?.NavigateToMainMenu());
        }

        public void NavigateToLobby()
        {
            Dispatch(() => GetMainViewModel()?.NavigateToLobby());
        }

        public void NavigateToGameBoard(
            bool isGuest = false,
            int targetCaptures = GameRules.DefaultCaptureGoal)
        {
            Dispatch(() => GetMainViewModel()?.NavigateToGameBoard(isGuest, targetCaptures));
        }

        public void NavigateToSharedGame(PrivateRoomNavigationContext navigationContext)
        {
            ArgumentNullException.ThrowIfNull(navigationContext);
            Dispatch(() => GetMainViewModel()?.NavigateToSharedGame(navigationContext));
        }

        public void NavigateToPublicGame(PublicMatchNavigationContext navigationContext)
        {
            ArgumentNullException.ThrowIfNull(navigationContext);
            Dispatch(() => GetMainViewModel()?.NavigateToPublicGame(navigationContext));
        }

        public void NavigateToProfileCustomization()
        {
            Dispatch(ShowProfileCustomization);
        }

        public void NavigateToCreatePrivateRoom()
        {
            Dispatch(ShowCreatePrivateRoom);
        }

        public void NavigateToJoinPrivateRoom()
        {
            Dispatch(ShowJoinPrivateRoom);
        }

        public void NavigateToPrivateRoomLobby(PrivateRoomNavigationContext navigationContext)
        {
            ArgumentNullException.ThrowIfNull(navigationContext);
            Dispatch(() => ShowPrivateRoomLobby(navigationContext));
        }

        public void NavigateToFriendSearch()
        {
            Dispatch(ShowFriendSearch);
        }

        public void NavigateToFriendRequests()
        {
            Dispatch(ShowFriendRequests);
        }

        public void NavigateToFriendList()
        {
            Dispatch(ShowFriendList);
        }

        public void OpenOptionsDialog()
        {
            Dispatch(() => GetMainViewModel()?.OpenOptionsDialog());
        }

        public void OpenDialog(DialogViewModelBase dialog)
        {
            Dispatch(() => GetMainViewModel()?.OpenDialog(dialog));
        }

        public void CloseDialog()
        {
            Dispatch(() => GetMainViewModel()?.CloseDialog());
        }

        public void ExitApplication()
        {
            Dispatch(() => Application.Current.Shutdown());
        }

        public void SignOut()
        {
            ShowLoginAfterSignOut(false);
        }

        public void SignOutAfterSessionExpired()
        {
            ShowLoginAfterSignOut(true);
        }

        public void OpenRegisterDialog()
        {
            Dispatch(() => GetLoginViewModel()?.OpenRegisterDialog());
        }

        public void OpenVerificationDialog(RegistrationDraft draft)
        {
            Dispatch(() => GetLoginViewModel()?.OpenVerificationDialog(draft));
        }

        public void ReturnToRegisterDialog()
        {
            Dispatch(() => GetLoginViewModel()?.ReturnToRegisterDialog());
        }

        public void DiscardRegistration()
        {
            Dispatch(() => GetLoginViewModel()?.DiscardRegistration());
        }

        public void ShowRegistrationResult(RegistrationResult result)
        {
            Dispatch(() => GetLoginViewModel()?.ShowRegistrationResult(result));
        }

        public void OpenForgotPasswordDialog()
        {
            Dispatch(() => GetLoginViewModel()?.OpenForgotPasswordDialog());
        }

        public void OpenForgotVerificationDialog()
        {
            Dispatch(() => GetLoginViewModel()?.OpenForgotVerificationDialog());
        }

        public void OpenNewPasswordDialog()
        {
            Dispatch(() => GetLoginViewModel()?.OpenNewPasswordDialog());
        }

        public void CloseLoginDialog()
        {
            Dispatch(() => GetLoginViewModel()?.CloseDialog());
        }

        private void ShowLoginAfterSignOut(bool sessionExpired)
        {
            Dispatch(() => ReplaceMainWindowWithLogin(sessionExpired));
        }

        private static MainViewModel? GetMainViewModel()
        {
            return Application.Current?.Windows
                .OfType<MainWindow>()
                .Select(window => window.DataContext as MainViewModel)
                .FirstOrDefault(viewModel => viewModel is not null);
        }

        private static LoginViewModel? GetLoginViewModel()
        {
            return Application.Current?.Windows
                .OfType<LoginView>()
                .Select(window => window.DataContext as LoginViewModel)
                .FirstOrDefault(viewModel => viewModel is not null);
        }

        private static void CloseLoginWindow(Window? except = null)
        {
            foreach (Window window in Application.Current.Windows
                .OfType<LoginView>()
                .Where(window => window != except)
                .ToList())
            {
                window.Close();
            }
        }

        private static void Dispatch(Action action)
        {
            Dispatcher? dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is null || dispatcher.CheckAccess())
            {
                action();
                return;
            }

            dispatcher.BeginInvoke(action);
        }

        private void ShowMainWindow(
            bool isGuest,
            string playerName,
            string? sessionToken,
            string? guestCredential)
        {
            var context = new ClientSessionContext
            {
                IsGuest = isGuest,
                PlayerName = playerName,
                SessionToken = sessionToken,
                GuestCredential = guestCredential
            };
            var mainWindow = new MainWindow(new MainViewModel(context, this));
            Application.Current.MainWindow = mainWindow;
            mainWindow.Show();
            CloseLoginWindow();
        }

        private void ReplaceLoginWindow()
        {
            LoginViewModel? loginViewModel = GetLoginViewModel();
            LoginView loginView = loginViewModel is null
                ? new LoginView()
                : new LoginView(loginViewModel);
            Application.Current.MainWindow = loginView;
            loginView.Show();
            CloseLoginWindow(loginView);
        }

        private void ShowProfileCustomization()
        {
            MainViewModel? mainViewModel = GetMainViewModel();
            if (mainViewModel is null)
            {
                return;
            }

            mainViewModel.CurrentViewModel = new ProfileCustomizationViewModel(
                this, mainViewModel.PlayerName);
        }

        private void ShowCreatePrivateRoom()
        {
            MainViewModel? mainViewModel = GetMainViewModel();
            if (mainViewModel is null || string.IsNullOrWhiteSpace(mainViewModel.SessionToken))
            {
                return;
            }

            var roomsClient = new GrpcRoomsClient(mainViewModel.SessionToken);
            mainViewModel.CurrentViewModel = new CreatePrivateRoomViewModel(this, roomsClient);
        }

        private void ShowJoinPrivateRoom()
        {
            MainViewModel? mainViewModel = GetMainViewModel();
            if (mainViewModel is null || string.IsNullOrWhiteSpace(mainViewModel.SessionToken))
            {
                return;
            }

            var roomsClient = new GrpcRoomsClient(mainViewModel.SessionToken);
            mainViewModel.CurrentViewModel = new JoinPrivateRoomViewModel(this, roomsClient);
        }

        private void ShowPrivateRoomLobby(PrivateRoomNavigationContext navigationContext)
        {
            MainViewModel? mainViewModel = GetMainViewModel();
            if (mainViewModel is null || string.IsNullOrWhiteSpace(mainViewModel.SessionToken))
            {
                return;
            }

            var roomsClient = new GrpcRoomsClient(mainViewModel.SessionToken);
            var lobby = new PrivateRoomLobbyViewModel(this, roomsClient, navigationContext);
            mainViewModel.CurrentViewModel = lobby;
            _ = lobby.WatchRoomAsync(navigationContext);
        }

        private void ShowFriendSearch()
        {
            MainViewModel? mainViewModel = GetMainViewModel();
            if (mainViewModel is null)
            {
                return;
            }

            mainViewModel.CurrentViewModel = new FriendSearchViewModel(
                this, mainViewModel.SessionToken);
        }

        private void ShowFriendRequests()
        {
            MainViewModel? mainViewModel = GetMainViewModel();
            if (mainViewModel is null)
            {
                return;
            }

            var context = new FriendRequestsContext
            {
                SessionToken = mainViewModel.SessionToken,
                Friends = mainViewModel.Friends
            };
            mainViewModel.CurrentViewModel = new FriendRequestsViewModel(this, context);
        }

        private void ShowFriendList()
        {
            MainViewModel? mainViewModel = GetMainViewModel();
            if (mainViewModel is null)
            {
                return;
            }

            mainViewModel.CurrentViewModel = new FriendListViewModel(this);
        }

        private void ReplaceMainWindowWithLogin(bool sessionExpired)
        {
            LoginView loginView = new LoginView();
            if (sessionExpired && loginView.DataContext is LoginViewModel loginViewModel)
            {
                loginViewModel.ShowSessionExpiredMessage();
            }

            Application.Current.MainWindow = loginView;
            loginView.Show();
            foreach (Window window in Application.Current.Windows
                .OfType<Window>()
                .Where(window => window is MainWindow)
                .ToList())
            {
                window.Close();
            }
        }
    }
}
