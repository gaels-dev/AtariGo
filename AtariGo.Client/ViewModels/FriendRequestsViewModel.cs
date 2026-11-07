using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

using Grpc.Core;

using AtariGo.Client.Commands;
using AtariGo.Client.Models;
using AtariGo.Client.Properties;
using AtariGo.Client.Services;
using AtariGo.Contracts;

namespace AtariGo.Client.ViewModels;

public sealed class FriendRequestsViewModel : ViewModelBase, IDisposable
{
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly CancellationToken _lifetimeToken;
    private bool _isDisposed;
    private readonly INavigationService _navigationService;
    private readonly IFriendClient? _friendClient;
    private string _statusMessage = string.Empty;
    private bool _isLoading;

    public FriendRequestsViewModel(
        INavigationService navigationService,
        FriendRequestsContext context,
        IFriendClient? friendClient = null)
    {
        ArgumentNullException.ThrowIfNull(navigationService);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.Friends);

        _lifetimeToken = _lifetimeCancellation.Token;
        _navigationService = navigationService;
        _friendClient = friendClient
            ?? (string.IsNullOrWhiteSpace(context.SessionToken)
                ? null
                : new GrpcFriendClient(context.SessionToken));

        Sent = new SentFriendRequestsViewModel(_friendClient);
        Requests = new ObservableCollection<IncomingFriendRequestViewModel>();
        RefreshCommand = new AsyncRelayCommand(
            _ => LoadRequestsAsync(),
            HandleServiceFailure,
            _lifetimeToken);
        BackCommand = new RelayCommand(
            _navigationService.NavigateToMainMenu);

        Initialization = LoadRequestsAsync();
    }

    public ObservableCollection<IncomingFriendRequestViewModel> Requests
    { get; }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public string TitleText => GetLocalizedMessage(
        "FriendRequests_Lbl_Title");

    public string RefreshLabel => GetLocalizedMessage(
        "FriendRequests_Btn_Refresh");

    public string AcceptLabel => GetLocalizedMessage(
        "FriendRequests_Btn_Accept");

    public string RejectLabel => GetLocalizedMessage(
        "FriendRequests_Btn_Reject");

    public string BackLabel => GetLocalizedMessage(
        "FriendSearch_Btn_Back");

    public SentFriendRequestsViewModel Sent { get; }

    public string IncomingLabel => GetLocalizedMessage("FriendRequests_Tab_Incoming");

    public string SentLabel => GetLocalizedMessage("FriendRequests_Tab_Sent");

    public Task Initialization { get; }

    public ICommand RefreshCommand { get; }

    public ICommand BackCommand { get; }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        Sent.Dispose();
        _lifetimeCancellation.Cancel();
        _lifetimeCancellation.Dispose();
    }

    private async Task LoadRequestsAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        if (IsLoading)
        {
            return;
        }

        if (_friendClient is null)
        {
            HandleServiceFailure(new InvalidOperationException(
                "An authenticated friend client is required."));
            return;
        }

        IsLoading = true;
        StatusMessage = GetLocalizedMessage(
            "FriendRequests_Msg_Loading");

        try
        {
            GetIncomingFriendRequestsResponse response =
                await _friendClient.GetIncomingFriendRequestsAsync(_lifetimeToken);

            _lifetimeToken.ThrowIfCancellationRequested();

            Requests.Clear();
            foreach (IncomingFriendRequest request in response.Requests)
            {
                Requests.Add(CreateRequestViewModel(request));
            }

            StatusMessage = Requests.Count == 0
                ? GetLocalizedMessage("FriendRequests_Msg_Empty")
                : string.Empty;
            await Sent.LoadAsync();
        }
        catch (Exception exception)
        {
            HandleServiceFailure(exception);
        }
        finally
        {
            if (!_isDisposed)
            {
                IsLoading = false;
            }
        }
    }

    private IncomingFriendRequestViewModel CreateRequestViewModel(
        IncomingFriendRequest request)
    {
        return new IncomingFriendRequestViewModel(
            request,
            RespondToRequestAsync,
            HandleServiceFailure);
    }

    private async Task RespondToRequestAsync(
        long requestId,
        bool accept)
    {
        if (_isDisposed)
        {
            return;
        }

        if (_friendClient is null)
        {
            HandleServiceFailure(new InvalidOperationException(
                "An authenticated friend client is required."));
            return;
        }

        try
        {
            RespondToFriendRequestResponse response =
                await _friendClient.RespondToFriendRequestAsync(
                    requestId,
                    accept,
                    _lifetimeToken);

            _lifetimeToken.ThrowIfCancellationRequested();
            ProcessResponse(response, requestId);
        }
        catch (Exception exception) when (GrpcCallPolicy.IsRequestedCancellation(
            exception, _lifetimeToken))
        {
            Trace.TraceInformation("Friend request response cancelled by its owner.");
        }
    }

    private void ProcessResponse(
        RespondToFriendRequestResponse response,
        long requestId)
    {
        // Membership follows the ordered session stream; replies may arrive after a removal.
        switch (response.Status)
        {
            case FriendRequestResponseStatus.Accepted:
                StatusMessage = GetLocalizedMessage(
                    "FriendRequests_Msg_Accepted");
                RemoveRequest(requestId);
                break;
            case FriendRequestResponseStatus.Rejected:
                StatusMessage = GetLocalizedMessage(
                    "FriendRequests_Msg_Rejected");
                RemoveRequest(requestId);
                break;
            case FriendRequestResponseStatus.AlreadyFriends:
                StatusMessage = GetLocalizedMessage(
                    "FriendRequests_Msg_AlreadyFriends");
                RemoveRequest(requestId);
                break;
            case FriendRequestResponseStatus.NotFound:
            case FriendRequestResponseStatus.AlreadyProcessed:
                StatusMessage = GetLocalizedMessage(
                    "FriendRequests_Msg_Stale");
                RemoveRequest(requestId);
                break;
            default:
                StatusMessage = GetLocalizedMessage(
                    "FriendRequests_Err_Service");
                break;
        }
    }

    private void RemoveRequest(long requestId)
    {
        IncomingFriendRequestViewModel? request = Requests
            .FirstOrDefault(candidate => candidate.RequestId == requestId);

        if (request is not null)
        {
            Requests.Remove(request);
        }
    }

    private void HandleServiceFailure(Exception exception)
    {
        if (_isDisposed || GrpcCallPolicy.IsRequestedCancellation(
                exception, _lifetimeToken))
        {
            return;
        }

        _ = GrpcFailureMessages.GetMessage(exception);

        StatusMessage = exception is RpcException rpcException
            && rpcException.StatusCode == StatusCode.Unauthenticated
                ? GetLocalizedMessage(
                    "FriendRequests_Err_SessionExpired")
                : GetLocalizedMessage("FriendRequests_Err_Service");
    }

    private static string GetLocalizedMessage(string key)
    {
        return Resources.ResourceManager.GetString(
            key,
            CultureInfo.CurrentUICulture)
            ?? key;
    }
}
