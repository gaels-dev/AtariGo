using AtariGo.Client.Models;
using AtariGo.Client.ViewModels.Dialogs;
using AtariGo.Contracts;

namespace AtariGo.Client.Services
{
    /// <summary>
    /// Provides navigation and window-level actions to view models without
    /// passing individual callbacks through the view-model hierarchy.
    /// </summary>
    public interface INavigationService
    {
        void NavigateToMainWindow(
            bool isGuest,
            string playerName,
            string? sessionToken = null,
            string? guestCredential = null);

        void RefreshLoginView();

        void NavigateToMainMenu();

        void NavigateToLobby();

        void NavigateToGameBoard(
            bool isGuest = false,
            int targetCaptures = GameRules.DefaultCaptureGoal);

        void NavigateToSharedGame(PrivateRoomNavigationContext navigationContext);

        void NavigateToPublicGame(PublicMatchNavigationContext navigationContext);

        void NavigateToProfileCustomization();

        void NavigateToCreatePrivateRoom();

        void NavigateToJoinPrivateRoom();

        void NavigateToPrivateRoomLobby(
            PrivateRoomNavigationContext navigationContext);

        void NavigateToFriendSearch();

        void NavigateToFriendRequests();

        void NavigateToFriendList();

        void OpenOptionsDialog();

        void OpenDialog(DialogViewModelBase dialog);

        void CloseDialog();

        void ExitApplication();

        void SignOut();

        void SignOutAfterSessionExpired();

        void OpenRegisterDialog();

        void OpenVerificationDialog(RegistrationDraft draft);

        void ReturnToRegisterDialog();

        void DiscardRegistration();

        void ShowRegistrationResult(RegistrationResult result);

        void OpenForgotPasswordDialog();

        void OpenForgotVerificationDialog();

        void OpenNewPasswordDialog();

        void CloseLoginDialog();
    }
}
