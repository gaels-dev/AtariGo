using System.Collections.ObjectModel;

using AtariGo.Client.ViewModels;

namespace AtariGo.Client.Models;

public sealed record RoomInvitationsNavigationContext(int RoomId)
{
    public const int IncomingRoomId = 0;

    public ObservableCollection<FriendItemViewModel> Friends { get; init; } = new();
}
