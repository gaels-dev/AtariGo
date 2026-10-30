using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace AtariGo.Client.ViewModels
{
    public sealed class RegisteredLobbyContext
    {
        public ObservableCollection<FriendItemViewModel> Friends { get; init; } =
            new ObservableCollection<FriendItemViewModel>();

        public Func<Task>? SignOutAsync { get; init; }
    }
}
