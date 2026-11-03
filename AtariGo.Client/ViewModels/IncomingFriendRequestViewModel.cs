using System.Threading.Tasks;
using System.Windows.Input;

using AtariGo.Client.Commands;
using AtariGo.Contracts;

namespace AtariGo.Client.ViewModels;

public sealed class IncomingFriendRequestViewModel : ViewModelBase
{
    private readonly Func<long, bool, Task> _respondAsync;
    private readonly Action<Exception> _onError;
    private bool _isResponding;

    public IncomingFriendRequestViewModel(
        IncomingFriendRequest request,
        Func<long, bool, Task> respondAsync,
        Action<Exception> onError)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(respondAsync);
        ArgumentNullException.ThrowIfNull(onError);

        RequestId = request.RequestId;
        RequesterUserId = request.RequesterUserId;
        RequesterUserName = request.RequesterUserName;
        _respondAsync = respondAsync;
        _onError = onError;

        AcceptCommand = new AsyncRelayCommand(
            _ => RespondAsync(true),
            _onError);
        RejectCommand = new AsyncRelayCommand(
            _ => RespondAsync(false),
            _onError);
    }

    public long RequestId { get; }

    public int RequesterUserId { get; }

    public string RequesterUserName { get; }

    public bool CanRespond => !_isResponding;

    public ICommand AcceptCommand { get; }

    public ICommand RejectCommand { get; }

    private async Task RespondAsync(bool accept)
    {
        if (_isResponding)
        {
            return;
        }

        _isResponding = true;
        OnPropertyChanged(nameof(CanRespond));

        try
        {
            await _respondAsync(RequestId, accept);
        }
        finally
        {
            _isResponding = false;
            OnPropertyChanged(nameof(CanRespond));
        }
    }
}
