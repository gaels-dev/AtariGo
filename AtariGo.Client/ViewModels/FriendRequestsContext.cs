using System.Collections.ObjectModel;

namespace AtariGo.Client.ViewModels;

public sealed class FriendRequestsContext
{
    public string? SessionToken { get; init; }

    public ObservableCollection<FriendItemViewModel> Friends { get; init; } = new();
}
