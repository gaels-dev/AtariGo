using System.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

using AtariGo.Server.Application.Sessions;
using AtariGo.Server.Application.Social;
using AtariGo.Server.Data;
using AtariGo.Server.Data.Entities;

namespace AtariGo.Server.Application.Friends;

public sealed class FriendService : IFriendService
{
    private const int MinimumSearchLength = 2;
    private const int MaximumSearchLength = 15;
    private const int MaximumSearchResults = 20;
    private const int MaximumPageSize = 100;
    private const string PendingRequestState =
        DatabaseValues.FriendRequestStates.Pending;

    private readonly AtariGoDbContext _dbContext;
    private readonly ISessionService _sessionService;
    private readonly FriendMutationCoordinator _mutations;
    private readonly ISessionConnectionRegistry _connectionRegistry;
    private readonly TimeProvider _timeProvider;

    public FriendService(
        AtariGoDbContext dbContext,
        ISessionService sessionService,
        ISessionConnectionRegistry connectionRegistry,
        TimeProvider timeProvider,
        FriendMutationCoordinator mutations)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(sessionService);
        ArgumentNullException.ThrowIfNull(mutations);
        ArgumentNullException.ThrowIfNull(connectionRegistry);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _dbContext = dbContext;
        _sessionService = sessionService;
        _mutations = mutations;
        _connectionRegistry = connectionRegistry;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<FriendCandidateEntry>?> SearchUsersAsync(
        string sessionToken,
        string query,
        CancellationToken cancellationToken)
    {
        string normalizedQuery = NormalizeQuery(query);
        int? requesterId = await FindActiveUserIdAsync(
            sessionToken,
            cancellationToken);

        if (requesterId is null)
        {
            return null;
        }

        return await FindMatchingUsersAsync(
            requesterId.Value,
            normalizedQuery,
            cancellationToken);
    }

    public async Task<SendFriendRequestResult?> SendFriendRequestAsync(
        string sessionToken,
        int recipientUserId,
        CancellationToken cancellationToken)
    {
        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        int? requesterId = await FindActiveUserIdAsync(
            sessionToken,
            cancellationToken);

        if (requesterId is null)
        {
            return null;
        }

        using FriendMutationLease lease = await _mutations.EnterAsync(cancellationToken);
        SendFriendRequestResult? rejection =
            await FindFriendRequestRejectionAsync(
                requesterId.Value,
                recipientUserId,
                cancellationToken);

        if (rejection is not null)
        {
            return rejection;
        }

        _dbContext.FriendRequests.Add(new FriendRequest
        {
            RequesterId = requesterId.Value,
            RecipientId = recipientUserId,
            State = PendingRequestState,
            RequestedAt = now
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        return SendFriendRequestResult.Sent;
    }

    public async Task<IReadOnlyList<IncomingFriendRequestEntry>?>
        GetIncomingFriendRequestsAsync(
            string sessionToken,
            CancellationToken cancellationToken)
    {
        int? recipientId = await FindActiveUserIdAsync(
            sessionToken,
            cancellationToken);

        if (recipientId is null)
        {
            return null;
        }

        return await FindIncomingFriendRequestsAsync(
            recipientId.Value,
            cancellationToken);
    }

    public async Task<FriendRequestResponseResult?> RespondToFriendRequestAsync(
        RespondToFriendRequestCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(command.RequestId);

        int? recipientId = await FindActiveUserIdAsync(
            command.SessionToken,
            cancellationToken);

        if (recipientId is null)
        {
            return null;
        }

        using FriendMutationLease lease = await _mutations.EnterAsync(cancellationToken);
        return await ApplyFriendRequestResponseAsync(
            command, recipientId.Value, cancellationToken);
    }

    public async Task<SentFriendRequestsPage?> ListFriendRequestsBySenderAsync(
        string sessionToken,
        SentFriendRequestsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.PageSize < 1 || query.PageSize > MaximumPageSize || query.AfterRequestId < 0)
        {
            throw new ArgumentException("Invalid request page.", nameof(query));
        }

        SessionIdentity? identity = await _sessionService.ValidateSessionAsync(
            sessionToken, cancellationToken);
        if (identity is null)
        {
            return null;
        }

        List<OutgoingFriendRequestEntry> requests = await _dbContext.FriendRequests
            .AsNoTracking()
            .Where(friendRequest => friendRequest.RequesterId == identity.UserId
                && friendRequest.State == PendingRequestState
                && friendRequest.FriendRequestId > query.AfterRequestId)
            .OrderBy(friendRequest => friendRequest.FriendRequestId)
            .Take(query.PageSize + 1)
            .Select(friendRequest => new OutgoingFriendRequestEntry(
                friendRequest.FriendRequestId,
                friendRequest.RecipientId,
                friendRequest.Recipient.UserName))
            .ToListAsync(cancellationToken);
        long? nextRequestId = null;
        if (requests.Count > query.PageSize)
        {
            requests.RemoveAt(query.PageSize);
            nextRequestId = requests[^1].RequestId;
        }

        return new SentFriendRequestsPage(requests, nextRequestId);
    }

    public async Task<CancelFriendRequestResult?> CancelFriendRequestAsync(
        string sessionToken,
        long requestId,
        CancellationToken cancellationToken)
    {
        if (requestId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(requestId));
        }

        SessionIdentity? identity = await _sessionService.ValidateSessionAsync(
            sessionToken, cancellationToken);
        if (identity is null)
        {
            return null;
        }

        using FriendMutationLease lease = await _mutations.EnterAsync(cancellationToken);
        int removed = await _dbContext.FriendRequests
            .Where(friendRequest => friendRequest.FriendRequestId == requestId
                && friendRequest.RequesterId == identity.UserId
                && friendRequest.State == PendingRequestState)
            .ExecuteDeleteAsync(cancellationToken);
        CancelFriendRequestResult result;
        if (removed > 0)
        {
            result = CancelFriendRequestResult.Cancelled;
        }
        else
        {
            result = CancelFriendRequestResult.NoLongerPending;
        }

        return result;
    }

    private static string NormalizeQuery(string query)
    {
        string normalizedQuery = query?.Trim() ?? string.Empty;
        if (normalizedQuery.Length < MinimumSearchLength
            || normalizedQuery.Length > MaximumSearchLength)
        {
            throw new ArgumentException(
                $"The search query must contain between {MinimumSearchLength} "
                    + $"and {MaximumSearchLength} characters.",
                nameof(query));
        }

        return normalizedQuery;
    }

    private async Task<int?> FindActiveUserIdAsync(
        string sessionToken,
        CancellationToken cancellationToken)
    {
        SessionIdentity? identity = await _sessionService.ValidateSessionAsync(
            sessionToken, cancellationToken);
        return identity?.UserId;
    }

    private Task<List<FriendCandidateEntry>> FindMatchingUsersAsync(
        int requesterId,
        string query,
        CancellationToken cancellationToken)
    {
        return _dbContext.Users
            .AsNoTracking()
            .Where(user => user.UserId != requesterId
                && user.State == DatabaseValues.AccountStates.Active
                && user.UserName.StartsWith(query)
                && !_dbContext.Friendships.Any(friendship =>
                    (friendship.UserId == requesterId
                     && friendship.FriendUserId == user.UserId)
                    || (friendship.UserId == user.UserId
                        && friendship.FriendUserId == requesterId))
                && !_dbContext.FriendRequests.Any(friendRequest =>
                    friendRequest.State == PendingRequestState
                    && ((friendRequest.RequesterId == requesterId
                         && friendRequest.RecipientId == user.UserId)
                        || (friendRequest.RequesterId == user.UserId
                            && friendRequest.RecipientId == requesterId))))
            .OrderBy(user => user.UserName)
            .Take(MaximumSearchResults)
            .Select(user => new FriendCandidateEntry(
                user.UserId,
                user.UserName))
            .ToListAsync(cancellationToken);
    }

    private Task<List<IncomingFriendRequestEntry>>
        FindIncomingFriendRequestsAsync(
            int recipientId,
            CancellationToken cancellationToken)
    {
        return _dbContext.FriendRequests
            .AsNoTracking()
            .Where(friendRequest => friendRequest.RecipientId == recipientId
                && friendRequest.State == PendingRequestState)
            .OrderByDescending(friendRequest => friendRequest.RequestedAt)
            .Select(friendRequest => new IncomingFriendRequestEntry(
                friendRequest.FriendRequestId,
                friendRequest.RequesterId,
                friendRequest.Requester.UserName))
            .ToListAsync(cancellationToken);
    }

    private async Task<FriendRequestResponseResult> ApplyFriendRequestResponseAsync(
        RespondToFriendRequestCommand command,
        int recipientUserId,
        CancellationToken cancellationToken)
    {
        await using IDbContextTransaction transaction = await _dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        FriendRequest? friendRequest = await FindFriendRequestAsync(
            command.RequestId, cancellationToken);
        FriendRequestResponseResult result;
        if (friendRequest is null || friendRequest.RecipientId != recipientUserId)
        {
            result = FriendRequestResponseResult.NotFound;
        }
        else if (command.Accept
            && friendRequest.Requester.State != DatabaseValues.AccountStates.Active)
        {
            result = FriendRequestResponseResult.NotFound;
        }
        else if (friendRequest.State != PendingRequestState)
        {
            result = FriendRequestResponseResult.AlreadyProcessed;
        }
        else
        {
            result = await CompleteFriendRequestAsync(
                friendRequest, command.Accept, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            if (result == FriendRequestResponseResult.Accepted)
            {
                _connectionRegistry.AddFriendship(
                    new SessionIdentity(
                        friendRequest.RequesterId, friendRequest.Requester.UserName),
                    new SessionIdentity(
                        friendRequest.RecipientId, friendRequest.Recipient.UserName));
            }
        }

        return result;
    }

    private Task<FriendRequest?> FindFriendRequestAsync(
        long requestId,
        CancellationToken cancellationToken)
    {
        return _dbContext.FriendRequests
            .Include(friendRequest => friendRequest.Requester)
            .Include(friendRequest => friendRequest.Recipient)
            .SingleOrDefaultAsync(
                friendRequest => friendRequest.FriendRequestId == requestId,
                cancellationToken);
    }

    private async Task<FriendRequestResponseResult> CompleteFriendRequestAsync(
        FriendRequest friendRequest,
        bool accept,
        CancellationToken cancellationToken)
    {
        bool alreadyFriends = accept && await AreFriendsAsync(
            friendRequest.RequesterId,
            friendRequest.RecipientId,
            cancellationToken);
        if (accept && !alreadyFriends)
        {
            DateTime createdAt = _timeProvider.GetUtcNow().UtcDateTime;
            AddFriendship(friendRequest.RequesterId, friendRequest.RecipientId, createdAt);
        }

        _dbContext.FriendRequests.Remove(friendRequest);
        await _dbContext.SaveChangesAsync(cancellationToken);
        FriendRequestResponseResult result;
        if (alreadyFriends)
        {
            result = FriendRequestResponseResult.AlreadyFriends;
        }
        else if (accept)
        {
            result = FriendRequestResponseResult.Accepted;
        }
        else
        {
            result = FriendRequestResponseResult.Rejected;
        }

        return result;
    }

    private void AddFriendship(
        int requesterId,
        int recipientId,
        DateTime createdAt)
    {
        _dbContext.Friendships.Add(new Friendship
        {
            UserId = Math.Min(requesterId, recipientId),
            FriendUserId = Math.Max(requesterId, recipientId),
            CreatedAt = createdAt
        });
    }

    private async Task<SendFriendRequestResult?> FindFriendRequestRejectionAsync(
        int requesterId,
        int recipientUserId,
        CancellationToken cancellationToken)
    {
        SendFriendRequestResult? rejection = null;
        if (requesterId == recipientUserId)
        {
            rejection = SendFriendRequestResult.CannotRequestSelf;
        }
        else if (!await _dbContext.Users.AnyAsync(
                user => user.UserId == recipientUserId
                && user.State == DatabaseValues.AccountStates.Active,
                cancellationToken))
        {
            rejection = SendFriendRequestResult.UserNotFound;
        }
        else if (await AreFriendsAsync(
                requesterId,
                recipientUserId,
                cancellationToken))
        {
            rejection = SendFriendRequestResult.AlreadyFriends;
        }
        else if (await HasPendingRequestAsync(
                requesterId,
                recipientUserId,
                cancellationToken))
        {
            rejection = SendFriendRequestResult.AlreadyPending;
        }

        return rejection;
    }

    private Task<bool> AreFriendsAsync(
        int requesterId,
        int recipientUserId,
        CancellationToken cancellationToken)
    {
        int firstUserId = Math.Min(requesterId, recipientUserId);
        int secondUserId = Math.Max(requesterId, recipientUserId);

        return _dbContext.Friendships.AnyAsync(
            friendship => friendship.UserId == firstUserId
                && friendship.FriendUserId == secondUserId,
            cancellationToken);
    }

    private Task<bool> HasPendingRequestAsync(
        int requesterId,
        int recipientUserId,
        CancellationToken cancellationToken)
    {
        return _dbContext.FriendRequests.AnyAsync(friendRequest =>
            friendRequest.State == PendingRequestState
            && ((friendRequest.RequesterId == requesterId
                 && friendRequest.RecipientId == recipientUserId)
                || (friendRequest.RequesterId == recipientUserId
                    && friendRequest.RecipientId == requesterId)),
            cancellationToken);
    }
}
