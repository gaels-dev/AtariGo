using System.Windows.Input;

using AtariGo.Contracts;

namespace AtariGo.Client.ViewModels;

public sealed class OutgoingFriendRequestViewModel : ViewModelBase
{
    public OutgoingFriendRequestViewModel(OutgoingFriendRequest request, ICommand cancelCommand)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(cancelCommand);
        RequestId = request.RequestId;
        RecipientUserName = request.RecipientUserName;
        CancelCommand = cancelCommand;
    }

    public long RequestId { get; }

    public string RecipientUserName { get; }

    public ICommand CancelCommand { get; }
}
