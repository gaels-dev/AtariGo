using Grpc.Core;

using AtariGo.Contracts;
using AtariGo.Server.Application.Friends;

namespace AtariGo.Server.Services;

public sealed class FriendDirectoryGrpcService : FriendDirectory.FriendDirectoryBase
{
    private const string AuthorizationHeader = "authorization";
    private const string BearerPrefix = "Bearer ";

    private readonly IFriendService _friendService;

    public FriendDirectoryGrpcService(IFriendService friendService)
    {
        ArgumentNullException.ThrowIfNull(friendService);
        _friendService = friendService;
    }

    private static string GetSessionToken(ServerCallContext context)
    {
        string? authorization = context.RequestHeaders.GetValue(
            AuthorizationHeader);

        if (authorization is null
            || !authorization.StartsWith(
                BearerPrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            throw CreateUnauthenticatedException();
        }

        string token = authorization[BearerPrefix.Length..].Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            throw CreateUnauthenticatedException();
        }

        return token;
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
            "La sesión no es válida o ha vencido."));
    }

    public override async Task<SearchUsersResponse> SearchUsers(
        SearchUsersRequest request,
        ServerCallContext context)
    {
        IReadOnlyList<FriendAccountEntry>? users;
        try
        {
            users = await _friendService.SearchUsersAsync(
                GetSessionToken(context),
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
                GetSessionToken(context),
                request.RecipientUserId,
                context.CancellationToken);

        if (result is null)
        {
            throw CreateUnauthenticatedException();
        }

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
                GetSessionToken(context),
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
                    GetSessionToken(context), request.RequestId, request.Accept),
                context.CancellationToken);

        if (result is null)
        {
            throw CreateUnauthenticatedException();
        }

        return new RespondToFriendRequestResponse
        {
            Status = MapResponseStatus(result.Value)
        };
    }
}
