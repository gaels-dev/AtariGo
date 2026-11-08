using System.Collections.ObjectModel;

using AtariGo.Client.ViewModels;

namespace AtariGo.Client.Models;

public sealed record RoomInvitationsNavigationContext(int RoomId, string RoomCode)
{
    public ObservableCollection<FriendItemViewModel> Friends { get; init; } = new();
}
