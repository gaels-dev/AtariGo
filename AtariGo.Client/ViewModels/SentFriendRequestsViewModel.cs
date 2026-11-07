using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;

using AtariGo.Client.Commands;
using AtariGo.Client.Properties;
using AtariGo.Client.Services;
using AtariGo.Contracts;

namespace AtariGo.Client.ViewModels;

public sealed class SentFriendRequestsViewModel : ViewModelBase, IDisposable
{
    private readonly IFriendClient? _client;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly CancellationToken _token;
    private string _nextPageToken = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _isBusy;
    private bool _isDisposed;

    public SentFriendRequestsViewModel(IFriendClient? client)
    {
        _client = client;
        _token = _lifetime.Token;
        LoadMoreCommand = new AsyncRelayCommand(_ => LoadAsync(true), HandleFailure, _token);
    }

    public ObservableCollection<OutgoingFriendRequestViewModel> Requests { get; } = new();

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool CanLoadMore => !_isBusy && !_isDisposed && _nextPageToken.Length > 0;

    public bool CanInteract => !_isBusy && !_isDisposed;

    public string CancelLabel => Localize("FriendRequests_Btn_Cancel");

    public string MoreLabel => Localize("FriendRequests_Btn_More");

    public ICommand LoadMoreCommand { get; }

    public async Task LoadAsync(bool append = false)
    {
        if (_isDisposed || _isBusy || (append && !CanLoadMore))
        {
            return;
        }

        SetBusy(true);
        StatusMessage = Localize("FriendRequests_Msg_Loading");
        try
        {
            await LoadPageAsync(append);
        }
        catch (Exception exception)
        {
            HandleFailure(exception);
        }
        finally
        {
            SetBusy(false);
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _lifetime.Cancel();
        _lifetime.Dispose();
    }

    private async Task LoadPageAsync(bool append)
    {
        if (_client is null)
        {
            throw new InvalidOperationException("An authenticated friend client is required.");
        }

        ListFriendRequestsBySenderResponse response = await _client.ListFriendRequestsBySenderAsync(
            append ? _nextPageToken : string.Empty, _token);
        _token.ThrowIfCancellationRequested();
        if (!append)
        {
            Requests.Clear();
        }

        foreach (OutgoingFriendRequest request in response.Requests)
        {
            if (!Requests.Any(existing => existing.RequestId == request.RequestId))
            {
                Requests.Add(CreateRequest(request));
            }
        }

        _nextPageToken = response.NextPageToken;
        StatusMessage = Requests.Count == 0
            ? Localize("FriendRequests_Msg_SentEmpty") : string.Empty;
    }

    private OutgoingFriendRequestViewModel CreateRequest(OutgoingFriendRequest request)
    {
        var cancel = new AsyncRelayCommand(
            _ => CancelAsync(request.RequestId), HandleFailure, _token);
        return new OutgoingFriendRequestViewModel(request, cancel);
    }

    private async Task CancelAsync(long requestId)
    {
        if (_isDisposed || _isBusy)
        {
            return;
        }

        SetBusy(true);
        try
        {
            await CancelRequestAsync(requestId);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task CancelRequestAsync(long requestId)
    {
        if (_client is null)
        {
            throw new InvalidOperationException("An authenticated friend client is required.");
        }

        CancelFriendRequestResponse response = await _client.CancelFriendRequestAsync(
            requestId, _token);
        _token.ThrowIfCancellationRequested();
        if (response.Status != CancelFriendRequestStatus.Cancelled
            && response.Status != CancelFriendRequestStatus.NoLongerPending)
        {
            throw new InvalidOperationException("Unexpected request cancellation result.");
        }

        OutgoingFriendRequestViewModel? request = Requests.FirstOrDefault(
            candidate => candidate.RequestId == requestId);
        if (request is not null)
        {
            Requests.Remove(request);
        }

        StatusMessage = Localize(response.Status == CancelFriendRequestStatus.Cancelled
            ? "FriendRequests_Msg_Cancelled" : "FriendRequests_Msg_NoLongerPending");
    }

    private void SetBusy(bool value)
    {
        if (_isDisposed)
        {
            return;
        }

        _isBusy = value;
        OnPropertyChanged(nameof(CanInteract));
        OnPropertyChanged(nameof(CanLoadMore));
    }

    private void HandleFailure(Exception exception)
    {
        if (_isDisposed || GrpcCallPolicy.IsRequestedCancellation(exception, _token))
        {
            return;
        }

        _ = GrpcFailureMessages.GetMessage(exception);
        StatusMessage = Localize("FriendRequests_Err_Unconfirmed");
    }

    private static string Localize(string key)
    {
        return Resources.ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
    }
}
