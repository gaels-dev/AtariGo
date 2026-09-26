using AtariGo.Client.ViewModels.Dialogs;

namespace AtariGo.Client.Services
{
    /// <summary>
    /// Provides navigation and window-level actions to view models without
    /// passing individual callbacks through the view-model hierarchy.
    /// </summary>
    public interface INavigationService
    {
        void NavigateToMainWindow(bool isGuest, string playerName);

        void RefreshLoginView();

        void NavigateToMainMenu();

        void NavigateToLobby();

        void NavigateToGameBoard(bool isGuest = false, int targetCaptures = 1);

        void OpenOptionsDialog();

        void OpenDialog(DialogViewModelBase dialog);

        void CloseDialog();

        void ExitApplication();

        void SignOut();

        void OpenRegisterDialog();

        void OpenVerificationDialog(string username);

        void OpenForgotPasswordDialog();

        void OpenForgotVerificationDialog();

        void OpenNewPasswordDialog();

        void CloseLoginDialog();
    }
}
