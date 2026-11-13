using System.Collections.ObjectModel;

namespace AtariGo.Client.ViewModels
{
    public sealed class RegisteredLobbyContext
    {
        public RoomInvitationsViewModel? RoomInvitations { get; init; }

        public string? SessionToken { get; init; }

        public ObservableCollection<FriendItemViewModel> Friends { get; init; } =
            new ObservableCollection<FriendItemViewModel>();

        public Func<Task>? SignOutAsync { get; init; }
    }
}
