using System.Collections.ObjectModel;

using AtariGo.Client.Services;

namespace AtariGo.Client.ViewModels
{
    public sealed class RegisteredLobbyContext
    {
        public RoomInvitationsViewModel? RoomInvitations { get; init; }

        public string? SessionToken { get; init; }

        public IProfileClient? ProfileClient { get; init; }

        public ObservableCollection<FriendItemViewModel> Friends { get; init; } =
            new ObservableCollection<FriendItemViewModel>();

        public Func<Task>? SignOutAsync { get; init; }
    }
}
