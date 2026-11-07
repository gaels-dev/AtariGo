using Grpc.Core;

using AtariGo.Contracts;
using AtariGo.Server.Application.Friends;

namespace AtariGo.Server.Services;

public sealed class FriendDirectoryGrpcService : FriendDirectory.FriendDirectoryBase
{
    private readonly IFriendService _friendService;
    private readonly ILogger<FriendDirectoryGrpcService> _logger;

    public FriendDirectoryGrpcService(
        IFriendService friendService,
        ILogger<FriendDirectoryGrpcService> logger)
    {
        ArgumentNullException.ThrowIfNull(friendService);
        ArgumentNullException.ThrowIfNull(logger);
        _friendService = friendService;
        _logger = logger;
    }

    private static SearchUsersResponse CreateSearchResponse(
        IReadOnlyList<FriendAccountEntry> users)
    {
        var response = new SearchUsersResponse();
        foreach (FriendAccountEntry user in users)
        {
            response.Users.Add(new FriendAccount
            {
                UserId = user.UserId,
                UserName = user.UserName
            });
        }

        return response;
    }

    private static FriendRequestStatus MapStatus(
        FriendRequestResult result)
    {
        return result switch
        {
            FriendRequestResult.Sent =>
                FriendRequestStatus.Sent,
            FriendRequestResult.AlreadyFriends =>
                FriendRequestStatus.AlreadyFriends,
            FriendRequestResult.AlreadyPending =>
                FriendRequestStatus.AlreadyPending,
            FriendRequestResult.UserNotFound =>
                FriendRequestStatus.UserNotFound,
            FriendRequestResult.CannotRequestSelf =>
                FriendRequestStatus.CannotRequestSelf,
            _ => FriendRequestStatus.Unspecified
        };
    }

    private static FriendRequestResponseStatus MapResponseStatus(
        FriendRequestResponseResult result)
    {
        return result switch
        {
            FriendRequestResponseResult.Accepted =>
                FriendRequestResponseStatus.Accepted,
            FriendRequestResponseResult.Rejected =>
                FriendRequestResponseStatus.Rejected,
            FriendRequestResponseResult.AlreadyFriends =>
                FriendRequestResponseStatus.AlreadyFriends,
            FriendRequestResponseResult.NotFound =>
                FriendRequestResponseStatus.NotFound,
            FriendRequestResponseResult.AlreadyProcessed =>
                FriendRequestResponseStatus.AlreadyProcessed,
            _ => FriendRequestResponseStatus.Unspecified
        };
    }

    private static RpcException CreateUnauthenticatedException()
    {
        return new RpcException(new Status(
            StatusCode.Unauthenticated,
            "The session is invalid or expired."));
    }

    public override async Task<SearchUsersResponse> SearchUsers(
        SearchUsersRequest request,
        ServerCallContext context)
    {
        IReadOnlyList<FriendAccountEntry>? users;
        try
        {
            users = await _friendService.SearchUsersAsync(
                SessionRequestAuthentication.GetSessionToken(context),
                request.Query,
                context.CancellationToken);
        }
        catch (ArgumentException exception)
        {
            throw new RpcException(new Status(
                StatusCode.InvalidArgument,
                exception.Message));
        }

        if (users is null)
        {
            throw CreateUnauthenticatedException();
        }

        return CreateSearchResponse(users);
    }

    public override async Task<SendFriendRequestResponse> SendFriendRequest(
        SendFriendRequestRequest request,
        ServerCallContext context)
    {
        FriendRequestResult? result =
            await _friendService.SendFriendRequestAsync(
                SessionRequestAuthentication.GetSessionToken(context),
                request.RecipientUserId,
                context.CancellationToken);

        if (result is null)
        {
            throw CreateUnauthenticatedException();
        }

        _logger.LogInformation("Friend request send result: {Status}.", result.Value);
        return new SendFriendRequestResponse
        {
            Status = MapStatus(result.Value)
        };
    }

    public override async Task<GetIncomingFriendRequestsResponse>
        GetIncomingFriendRequests(
            GetIncomingFriendRequestsRequest request,
            ServerCallContext context)
    {
        IReadOnlyList<IncomingFriendRequestEntry>? requests =
            await _friendService.GetIncomingFriendRequestsAsync(
                SessionRequestAuthentication.GetSessionToken(context),
                context.CancellationToken);

        if (requests is null)
        {
            throw CreateUnauthenticatedException();
        }

        var response = new GetIncomingFriendRequestsResponse();
        foreach (IncomingFriendRequestEntry incomingRequest in requests)
        {
            response.Requests.Add(new IncomingFriendRequest
            {
                RequestId = incomingRequest.RequestId,
                RequesterUserId = incomingRequest.RequesterUserId,
                RequesterUserName = incomingRequest.RequesterUserName
            });
        }

        return response;
    }

    public override async Task<RespondToFriendRequestResponse>
        RespondToFriendRequest(
            RespondToFriendRequestRequest request,
            ServerCallContext context)
    {
        if (request.RequestId <= 0)
        {
            throw new RpcException(new Status(
                StatusCode.InvalidArgument,
                "The friend request identifier must be positive."));
        }

        FriendRequestResponseResult? result =
            await _friendService.RespondToFriendRequestAsync(
                new RespondToFriendRequestCommand(
                    SessionRequestAuthentication.GetSessionToken(context),
                    request.RequestId,
                    request.Accept),
                context.CancellationToken);

        if (result is null)
        {
            throw CreateUnauthenticatedException();
        }

        _logger.LogInformation("Friend request {RequestId} response: {Status}.",
            request.RequestId, result.Value);
        return new RespondToFriendRequestResponse
        {
            Status = MapResponseStatus(result.Value)
        };
    }

    public override async Task<ListFriendRequestsBySenderResponse> ListFriendRequestsBySender(
        ListFriendRequestsBySenderRequest request,
        ServerCallContext context)
    {
        string token = SessionRequestAuthentication.GetSessionToken(context);
        if (request.Filter != FriendRequestFilter.Unspecified
            && request.Filter != FriendRequestFilter.Pending)
        {
            throw new RpcException(new Status(
                StatusCode.InvalidArgument, "Only pending requests can be listed."));
        }

        SentFriendRequestsQuery query = FriendRequestPageTokens.Parse(request.Page);
        SentFriendRequestsPage? page = await _friendService.ListFriendRequestsBySenderAsync(
            token, query, context.CancellationToken);
        if (page is null)
        {
            throw CreateUnauthenticatedException();
        }

        var response = new ListFriendRequestsBySenderResponse
        {
            NextPageToken = FriendRequestPageTokens.Encode(page.NextRequestId)
        };
        foreach (OutgoingFriendRequestEntry entry in page.Requests)
        {
            response.Requests.Add(new OutgoingFriendRequest
            {
                RequestId = entry.RequestId,
                RecipientUserId = entry.RecipientUserId,
                RecipientUserName = entry.RecipientUserName
            });
        }

        return response;
    }

    public override async Task<CancelFriendRequestResponse> CancelFriendRequest(
        CancelFriendRequestRequest request,
        ServerCallContext context)
    {
        string token = SessionRequestAuthentication.GetSessionToken(context);
        if (request.RequestId <= 0)
        {
            throw new RpcException(new Status(
                StatusCode.InvalidArgument, "The friend request identifier must be positive."));
        }

        CancelFriendRequestResult? result = await _friendService.CancelFriendRequestAsync(
            token, request.RequestId, context.CancellationToken);
        if (result is null)
        {
            throw CreateUnauthenticatedException();
        }

        _logger.LogInformation("Friend request {RequestId} cancellation: {Status}.",
            request.RequestId, result.Value);
        return new CancelFriendRequestResponse
        {
            Status = result == CancelFriendRequestResult.Cancelled
                ? CancelFriendRequestStatus.Cancelled
                : CancelFriendRequestStatus.NoLongerPending
        };
    }
}
