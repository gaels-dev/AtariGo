using System.Data.Common;

using Microsoft.EntityFrameworkCore;

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
        IReadOnlyList<FriendCandidateEntry> users)
    {
        var response = new SearchUsersResponse();
        foreach (FriendCandidateEntry user in users)
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
        SendFriendRequestResult result)
    {
        return result switch
        {
            SendFriendRequestResult.Sent =>
                FriendRequestStatus.Sent,
            SendFriendRequestResult.AlreadyFriends =>
                FriendRequestStatus.AlreadyFriends,
            SendFriendRequestResult.AlreadyPending =>
                FriendRequestStatus.AlreadyPending,
            SendFriendRequestResult.UserNotFound =>
                FriendRequestStatus.UserNotFound,
            SendFriendRequestResult.CannotRequestSelf =>
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

    private static StatusCode GetDatabaseFailureStatus(DbException? databaseFailure)
    {
        StatusCode status = StatusCode.Internal;
        if (databaseFailure is not null && databaseFailure.IsTransient)
        {
            status = StatusCode.Unavailable;
        }

        return status;
    }

    private async Task<TResult> ExecuteFriendOperationAsync<TResult>(
        string operationName,
        Func<Task<TResult>> operation)
    {
        TResult result;
        try
        {
            result = await operation();
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw CreatePersistenceFailure(operationName, StatusCode.Aborted,
                exception.GetType().Name);
        }
        catch (DbUpdateException exception)
        {
            throw CreatePersistenceFailure(operationName,
                GetDatabaseFailureStatus(exception.InnerException as DbException),
                exception.GetType().Name);
        }
        catch (DbException exception)
        {
            throw CreatePersistenceFailure(operationName, GetDatabaseFailureStatus(exception),
                exception.GetType().Name);
        }

        return result;
    }

    private RpcException CreatePersistenceFailure(
        string operationName,
        StatusCode status,
        string errorType)
    {
        _logger.LogError("Friend operation {Operation} failed with {ErrorType}: {Status}.",
            operationName, errorType, status);
        string message = "The friend operation could not be completed or confirmed.";
        if (status == StatusCode.Aborted)
        {
            message = "The friend data changed during the operation.";
        }

        return new RpcException(new Status(status, message));
    }

    public override async Task<SearchUsersResponse> SearchUsers(
        SearchUsersRequest searchUsersRequest,
        ServerCallContext context)
    {
        string token = SessionRequestAuthentication.GetSessionToken(context);
        IReadOnlyList<FriendCandidateEntry>? users;
        try
        {
            users = await ExecuteFriendOperationAsync(nameof(IFriendService.SearchUsersAsync),
                () => _friendService.SearchUsersAsync(
                    token, searchUsersRequest.Query, context.CancellationToken));
        }
        catch (ArgumentException exception) when (exception.ParamName == "query")
        {
            _logger.LogWarning("Friend search rejected invalid search text.");
            throw new RpcException(new Status(
                StatusCode.InvalidArgument,
                "The search text is invalid."));
        }

        if (users is null)
        {
            throw CreateUnauthenticatedException();
        }

        return CreateSearchResponse(users);
    }

    public override async Task<SendFriendRequestResponse> SendFriendRequest(
        SendFriendRequestRequest sendFriendRequest,
        ServerCallContext context)
    {
        string token = SessionRequestAuthentication.GetSessionToken(context);
        SendFriendRequestResult? result = await ExecuteFriendOperationAsync(
            nameof(IFriendService.SendFriendRequestAsync),
            () => _friendService.SendFriendRequestAsync(
                token, sendFriendRequest.RecipientUserId, context.CancellationToken));

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
            GetIncomingFriendRequestsRequest incomingFriendRequestsQuery,
            ServerCallContext context)
    {
        string token = SessionRequestAuthentication.GetSessionToken(context);
        IReadOnlyList<IncomingFriendRequestEntry>? requests = await ExecuteFriendOperationAsync(
            nameof(IFriendService.GetIncomingFriendRequestsAsync),
            () => _friendService.GetIncomingFriendRequestsAsync(token, context.CancellationToken));

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
            RespondToFriendRequestRequest friendRequestResponse,
            ServerCallContext context)
    {
        var command = new RespondToFriendRequestCommand(
            SessionRequestAuthentication.GetSessionToken(context),
            friendRequestResponse.RequestId,
            friendRequestResponse.Accept);
        FriendRequestResponseResult? result;
        try
        {
            result = await ExecuteFriendOperationAsync(
                nameof(IFriendService.RespondToFriendRequestAsync),
                () => _friendService.RespondToFriendRequestAsync(
                    command, context.CancellationToken));
        }
        catch (ArgumentOutOfRangeException exception)
            when (exception.ParamName == "command.RequestId")
        {
            _logger.LogWarning("Friend response rejected a nonpositive identifier.");
            throw new RpcException(new Status(
                StatusCode.InvalidArgument,
                "The friend request identifier must be positive."));
        }

        if (result is null)
        {
            throw CreateUnauthenticatedException();
        }

        _logger.LogInformation("Friend request {RequestId} response: {Status}.",
            friendRequestResponse.RequestId, result.Value);
        return new RespondToFriendRequestResponse
        {
            Status = MapResponseStatus(result.Value)
        };
    }

    public override async Task<ListFriendRequestsBySenderResponse> ListFriendRequestsBySender(
        ListFriendRequestsBySenderRequest sentFriendRequestsQuery,
        ServerCallContext context)
    {
        string token = SessionRequestAuthentication.GetSessionToken(context);
        if (sentFriendRequestsQuery.Filter != FriendRequestFilter.Unspecified
            && sentFriendRequestsQuery.Filter != FriendRequestFilter.Pending)
        {
            throw new RpcException(new Status(
                StatusCode.InvalidArgument, "Only pending requests can be listed."));
        }

        SentFriendRequestsQuery query = FriendRequestPageTokens.Parse(sentFriendRequestsQuery.Page);
        SentFriendRequestsPage? page = await ExecuteFriendOperationAsync(
            nameof(IFriendService.ListFriendRequestsBySenderAsync),
            () => _friendService.ListFriendRequestsBySenderAsync(
                token, query, context.CancellationToken));
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
        CancelFriendRequestRequest cancelFriendRequest,
        ServerCallContext context)
    {
        string token = SessionRequestAuthentication.GetSessionToken(context);
        if (cancelFriendRequest.RequestId <= 0)
        {
            throw new RpcException(new Status(
                StatusCode.InvalidArgument, "The friend request identifier must be positive."));
        }

        CancelFriendRequestResult? result = await ExecuteFriendOperationAsync(
            nameof(IFriendService.CancelFriendRequestAsync),
            () => _friendService.CancelFriendRequestAsync(
                token, cancelFriendRequest.RequestId, context.CancellationToken));
        if (result is null)
        {
            throw CreateUnauthenticatedException();
        }

        _logger.LogInformation("Friend request {RequestId} cancellation: {Status}.",
            cancelFriendRequest.RequestId, result.Value);
        CancelFriendRequestStatus status;
        if (result == CancelFriendRequestResult.Cancelled)
        {
            status = CancelFriendRequestStatus.Cancelled;
        }
        else
        {
            status = CancelFriendRequestStatus.NoLongerPending;
        }

        return new CancelFriendRequestResponse { Status = status };
    }
}
