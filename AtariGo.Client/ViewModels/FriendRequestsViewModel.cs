using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Input;

using AtariGo.Client.Commands;
using AtariGo.Client.Models;
using AtariGo.Client.Properties;
using AtariGo.Client.Services;
using AtariGo.Contracts;
using Grpc.Core;

namespace AtariGo.Client.ViewModels;

public sealed class FriendRequestsViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;
    private readonly IFriendClient? _friendClient;
    private readonly ObservableCollection<FriendItemViewModel> _friends;
    private string _statusMessage = string.Empty;
    private bool _isLoading;

    public FriendRequestsViewModel(
        INavigationService navigationService,
        string? sessionToken,
        ObservableCollection<FriendItemViewModel> friends,
        IFriendClient? friendClient = null)
    {
        ArgumentNullException.ThrowIfNull(navigationService);
        ArgumentNullException.ThrowIfNull(friends);

        _navigationService = navigationService;
        _friends = friends;
        _friendClient = friendClient
            ?? (string.IsNullOrWhiteSpace(sessionToken)
                ? null
                : new GrpcFriendClient(sessionToken));

        Requests = new ObservableCollection<IncomingFriendRequestViewModel>();
        RefreshCommand = new AsyncRelayCommand(
            _ => LoadRequestsAsync(),
            HandleServiceFailure);
        BackCommand = new RelayCommand(
            _navigationService.NavigateToMainMenu);

        _ = LoadRequestsAsync();
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

    public ICommand RefreshCommand { get; }

    public ICommand BackCommand { get; }

    private async Task LoadRequestsAsync()
    {
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
                await _friendClient.GetIncomingFriendRequestsAsync();

            Requests.Clear();
            foreach (IncomingFriendRequest request in response.Requests)
            {
                Requests.Add(CreateRequestViewModel(request));
            }

            StatusMessage = Requests.Count == 0
                ? GetLocalizedMessage("FriendRequests_Msg_Empty")
                : string.Empty;
        }
        catch (Exception exception)
        {
            HandleServiceFailure(exception);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private IncomingFriendRequestViewModel CreateRequestViewModel(
        IncomingFriendRequest request)
    {
        return new IncomingFriendRequestViewModel(
            request.RequestId,
            request.RequesterUserId,
            request.RequesterUserName,
            RespondToRequestAsync,
            HandleServiceFailure);
    }

    private async Task RespondToRequestAsync(
        long requestId,
        bool accept)
    {
        if (_friendClient is null)
        {
            HandleServiceFailure(new InvalidOperationException(
                "An authenticated friend client is required."));
            return;
        }

        RespondToFriendRequestResponse response =
            await _friendClient.RespondToFriendRequestAsync(
                requestId,
                accept);

        ProcessResponse(response, requestId);
    }

    private void ProcessResponse(
        RespondToFriendRequestResponse response,
        long requestId)
    {
        switch (response.Status)
        {
            case FriendRequestResponseStatus.Accepted:
                AddFriendFromRequest(requestId);
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
                AddFriendFromRequest(requestId);
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

    private void AddFriendFromRequest(long requestId)
    {
        IncomingFriendRequestViewModel? request = Requests
            .FirstOrDefault(candidate => candidate.RequestId == requestId);

        if (request is null || _friends.Any(friend =>
                friend.UserId == request.RequesterUserId))
        {
            return;
        }

        _friends.Add(new FriendItemViewModel(
            request.RequesterUserId,
            request.RequesterUserName,
            isOnline: false));
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
