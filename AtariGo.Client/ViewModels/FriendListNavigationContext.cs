using System.Collections.ObjectModel;

namespace AtariGo.Client.ViewModels;

public sealed class FriendListNavigationContext
{
    public string? SessionToken { get; init; }

    public ObservableCollection<FriendItemViewModel> Friends { get; init; } = new();
}
