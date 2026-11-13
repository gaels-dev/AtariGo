using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;

using AtariGo.Client.Commands;
using AtariGo.Client.Services;

namespace AtariGo.Client.ViewModels;

public sealed class FriendListViewModel : ViewModelBase, IDisposable
{
    public FriendListViewModel(
        INavigationService navigation,
        FriendListNavigationContext context,
        IFriendshipsClient? client = null)
    {
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(context);
        Friends = context.Friends;
        FriendRemoval = new FriendRemovalViewModel(navigation, context, client);
        BackCommand = new RelayCommand(navigation.NavigateToMainMenu);
    }

    public ObservableCollection<FriendItemViewModel> Friends { get; }

    public FriendRemovalViewModel FriendRemoval { get; }

    public string EmptyLabel => Properties.Resources.ResourceManager.GetString(
        "Presence_Msg_Empty", CultureInfo.CurrentUICulture) ?? string.Empty;

    public ICommand BackCommand { get; }

    public void Dispose()
    {
        FriendRemoval.Dispose();
    }
}
