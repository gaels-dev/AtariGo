using System;
using System.Linq;
using System.Windows;

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
            string? sessionToken = null) =>
            Dispatch(() =>
            {
                MainWindow mainWindow = new MainWindow(
                    new MainViewModel(
                        isGuest,
                        playerName,
                        this,
                        sessionToken));

                Application.Current.MainWindow = mainWindow;
                mainWindow.Show();
                CloseLoginWindow();
            });

        public void RefreshLoginView() =>
            Dispatch(() =>
            {
                LoginViewModel? loginViewModel = GetLoginViewModel();

                LoginView loginView = loginViewModel is null
                    ? new LoginView()
                    : new LoginView(loginViewModel);

                Application.Current.MainWindow = loginView;
                loginView.Show();
                CloseLoginWindow(loginView);
            });

        public void NavigateToMainMenu() =>
            Dispatch(() =>
                GetMainViewModel()?.NavigateToMainMenu());

        public void NavigateToLobby() =>
            Dispatch(() =>
                GetMainViewModel()?.NavigateToLobby());

        public void NavigateToGameBoard(
            bool isGuest = false,
            int targetCaptures = 1) =>
            Dispatch(() =>
                GetMainViewModel()?.NavigateToGameBoard(
                    isGuest,
                    targetCaptures));

        public void NavigateToProfileCustomization() =>
            Dispatch(() =>
            {
                MainViewModel? mainViewModel = GetMainViewModel();

                if (mainViewModel is null)
                {
                    return;
                }

                mainViewModel.CurrentViewModel =
                    new ProfileCustomizationViewModel(
                        this,
                        mainViewModel.PlayerName);
            });

        public void NavigateToCreatePrivateRoom() =>
            Dispatch(() =>
            {
                MainViewModel? mainViewModel = GetMainViewModel();

                if (mainViewModel is null)
                {
                    return;
                }

                mainViewModel.CurrentViewModel =
                    new CreatePrivateRoomViewModel(this);
            });

        public void NavigateToJoinPrivateRoom() =>
            Dispatch(() =>
            {
                MainViewModel? mainViewModel = GetMainViewModel();

                if (mainViewModel is null)
                {
                    return;
                }

                mainViewModel.CurrentViewModel =
                    new JoinPrivateRoomViewModel(this);
            });

        public void NavigateToPrivateRoomLobby(
            PrivateRoomNavigationContext navigationContext)
        {
            ArgumentNullException.ThrowIfNull(navigationContext);

            Dispatch(() =>
            {
                MainViewModel? mainViewModel = GetMainViewModel();

                if (mainViewModel is null)
                {
                    return;
                }

                mainViewModel.CurrentViewModel =
                    new PrivateRoomLobbyViewModel(
                        this,
                        navigationContext);
            });
        }

        public void NavigateToFriendSearch() =>
            Dispatch(() =>
            {
                MainViewModel? mainViewModel = GetMainViewModel();

                if (mainViewModel is null)
                {
                    return;
                }

                mainViewModel.CurrentViewModel =
                    new FriendSearchViewModel(this);
            });

        public void NavigateToFriendList(
            string roomCode,
            int targetCaptures) =>
            Dispatch(() =>
            {
                MainViewModel? mainViewModel = GetMainViewModel();

                if (mainViewModel is null)
                {
                    return;
                }

                mainViewModel.CurrentViewModel =
                    new FriendListViewModel(
                        this,
                        roomCode,
                        targetCaptures);
            });

        public void OpenOptionsDialog() =>
            Dispatch(() =>
                GetMainViewModel()?.OpenOptionsDialog());

        public void OpenDialog(DialogViewModelBase dialog) =>
            Dispatch(() =>
                GetMainViewModel()?.OpenDialog(dialog));

        public void CloseDialog() =>
            Dispatch(() =>
                GetMainViewModel()?.CloseDialog());

        public void ExitApplication() =>
            Dispatch(() =>
                Application.Current.Shutdown());

        public void SignOut() =>
            Dispatch(() =>
            {
                LoginView loginView = new LoginView();

                Application.Current.MainWindow = loginView;
                loginView.Show();

                foreach (Window window in Application.Current.Windows
                             .OfType<Window>()
                             .Where(window => window is MainWindow)
                             .ToList())
                {
                    window.Close();
                }
            });

        public void OpenRegisterDialog() =>
            Dispatch(() =>
                GetLoginViewModel()?.OpenRegisterDialog());

        public void OpenVerificationDialog(
            RegistrationDraft draft) =>
            Dispatch(() =>
                GetLoginViewModel()?.OpenVerificationDialog(
                    draft));

        public void ReturnToRegisterDialog() =>
            Dispatch(() =>
                GetLoginViewModel()?.ReturnToRegisterDialog());

        public void DiscardRegistration() =>
            Dispatch(() =>
                GetLoginViewModel()?.DiscardRegistration());

        public void ShowRegistrationResult(
            RegistrationResult result) =>
            Dispatch(() =>
                GetLoginViewModel()?.ShowRegistrationResult(
                    result));

        public void OpenForgotPasswordDialog() =>
            Dispatch(() =>
                GetLoginViewModel()?.OpenForgotPasswordDialog());

        public void OpenForgotVerificationDialog() =>
            Dispatch(() =>
                GetLoginViewModel()?.OpenForgotVerificationDialog());

        public void OpenNewPasswordDialog() =>
            Dispatch(() =>
                GetLoginViewModel()?.OpenNewPasswordDialog());

        public void CloseLoginDialog() =>
            Dispatch(() =>
                GetLoginViewModel()?.CloseDialog());

        private static MainViewModel? GetMainViewModel() =>
            Application.Current?.Windows
                .OfType<MainWindow>()
                .Select(
                    window =>
                        window.DataContext as MainViewModel)
                .FirstOrDefault(
                    viewModel => viewModel is not null);

        private static LoginViewModel? GetLoginViewModel() =>
            Application.Current?.Windows
                .OfType<LoginView>()
                .Select(
                    window =>
                        window.DataContext as LoginViewModel)
                .FirstOrDefault(
                    viewModel => viewModel is not null);

        private static void CloseLoginWindow(
            Window? except = null)
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
            var dispatcher = Application.Current?.Dispatcher;

            if (dispatcher is null || dispatcher.CheckAccess())
            {
                action();
                return;
            }

            dispatcher.BeginInvoke(action);
        }
    }
}