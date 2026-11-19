using AtariGo.Client.Models;
using AtariGo.Client.ViewModels.Dialogs;

namespace AtariGo.Client.Services
{
    public interface INavigationService
    {
        void NavigateToMainWindow(ClientSessionContext context);

        void RefreshLoginView();

        void NavigateToMainMenu();

        void NavigateToLobby();

        void NavigateToSharedGame(PrivateRoomNavigationContext navigationContext);

        void NavigateToPublicGame(PublicMatchNavigationContext navigationContext);

        void NavigateToProfileCustomization();

        void NavigateToCreatePrivateRoom();

        void NavigateToCreatePrivateRoomForInvitation(int recipientUserId);

        void NavigateToJoinPrivateRoom();

        void NavigateToPrivateRoomLobby(
            PrivateRoomNavigationContext navigationContext);

        void NavigateToFriendSearch();

        void NavigateToFriendRequests();

        void NavigateToGameHistory();

        void OpenOptionsDialog();

        void OpenDialog(DialogViewModelBase dialog);

        void CloseDialog();

        void ExitApplication();

        void SignOut();

        void SignOutAfterSessionExpired();

        void OpenRegisterDialog();

        void ShowRegistrationSuccess();

        void OpenForgotPasswordDialog();

        void OpenChangePasswordDialog();

        void CloseLoginDialog();
    }
}
