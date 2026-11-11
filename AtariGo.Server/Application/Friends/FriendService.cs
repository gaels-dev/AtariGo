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

    public async Task<IReadOnlyList<FriendAccountEntry>?> SearchUsersAsync(
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

    public async Task<FriendRequestResult?> SendFriendRequestAsync(
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
        FriendRequestResult? validationResult =
            await ValidateFriendRequestAsync(
                requesterId.Value,
                recipientUserId,
                cancellationToken);

        if (validationResult is not null)
        {
            return validationResult;
        }

        _dbContext.FriendRequests.Add(new FriendRequest
        {
            RequesterId = requesterId.Value,
            RecipientId = recipientUserId,
            State = PendingRequestState,
            RequestedAt = now
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        return FriendRequestResult.Sent;
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

        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        int? recipientId = await FindActiveUserIdAsync(
            command.SessionToken,
            cancellationToken);

        if (recipientId is null)
        {
            return null;
        }

        using FriendMutationLease lease = await _mutations.EnterAsync(cancellationToken);
        var decision = new FriendRequestDecision
        {
            RecipientUserId = recipientId.Value,
            RequestId = command.RequestId,
            Accept = command.Accept,
            RecordedAt = now
        };
        return await ApplyFriendRequestResponseAsync(decision, cancellationToken);
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
            .Where(request => request.RequesterId == identity.UserId
                && request.State == PendingRequestState
                && request.FriendRequestId > query.AfterRequestId)
            .OrderBy(request => request.FriendRequestId)
            .Take(query.PageSize + 1)
            .Select(request => new OutgoingFriendRequestEntry(
                request.FriendRequestId, request.RecipientId, request.Recipient.UserName))
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
            .Where(request => request.FriendRequestId == requestId
                && request.RequesterId == identity.UserId
                && request.State == PendingRequestState)
            .ExecuteDeleteAsync(cancellationToken);
        return removed > 0
            ? CancelFriendRequestResult.Cancelled
            : CancelFriendRequestResult.NoLongerPending;
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

    private Task<List<FriendAccountEntry>> FindMatchingUsersAsync(
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
                && !_dbContext.FriendRequests.Any(request =>
                    request.State == PendingRequestState
                    && ((request.RequesterId == requesterId
                         && request.RecipientId == user.UserId)
                        || (request.RequesterId == user.UserId
                            && request.RecipientId == requesterId))))
            .OrderBy(user => user.UserName)
            .Take(MaximumSearchResults)
            .Select(user => new FriendAccountEntry(
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
            .Where(request => request.RecipientId == recipientId
                && request.State == PendingRequestState)
            .OrderByDescending(request => request.RequestedAt)
            .Select(request => new IncomingFriendRequestEntry(
                request.FriendRequestId,
                request.RequesterId,
                request.Requester.UserName))
            .ToListAsync(cancellationToken);
    }

    private async Task<FriendRequestResponseResult> ApplyFriendRequestResponseAsync(
        FriendRequestDecision decision,
        CancellationToken cancellationToken)
    {
        await using IDbContextTransaction transaction = await _dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        FriendRequest? request = await FindRequestAsync(decision.RequestId, cancellationToken);
        if (request is null || request.RecipientId != decision.RecipientUserId)
        {
            return FriendRequestResponseResult.NotFound;
        }

        if (decision.Accept
            && request.Requester.State != DatabaseValues.AccountStates.Active)
        {
            return FriendRequestResponseResult.NotFound;
        }

        if (request.State != PendingRequestState)
        {
            return FriendRequestResponseResult.AlreadyProcessed;
        }

        FriendRequestResponseResult result = await SaveFriendRequestResponseAsync(
            request,
            decision,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        if (result == FriendRequestResponseResult.Accepted)
        {
            _connectionRegistry.AddFriendship(
                new SessionIdentity(request.RequesterId, request.Requester.UserName),
                new SessionIdentity(request.RecipientId, request.Recipient.UserName));
        }

        return result;
    }

    private Task<FriendRequest?> FindRequestAsync(
        long requestId,
        CancellationToken cancellationToken)
    {
        return _dbContext.FriendRequests
            .Include(request => request.Requester)
            .Include(request => request.Recipient)
            .SingleOrDefaultAsync(
                request => request.FriendRequestId == requestId,
                cancellationToken);
    }

    private async Task<FriendRequestResponseResult> SaveFriendRequestResponseAsync(
        FriendRequest request,
        FriendRequestDecision decision,
        CancellationToken cancellationToken)
    {
        bool alreadyFriends = decision.Accept && await AreFriendsAsync(
            request.RequesterId,
            decision.RecipientUserId,
            cancellationToken);
        if (decision.Accept && !alreadyFriends)
        {
            AddFriendship(request.RequesterId, decision.RecipientUserId, decision.RecordedAt);
        }

        _dbContext.FriendRequests.Remove(request);
        await _dbContext.SaveChangesAsync(cancellationToken);
        if (alreadyFriends)
        {
            return FriendRequestResponseResult.AlreadyFriends;
        }

        return decision.Accept
            ? FriendRequestResponseResult.Accepted
            : FriendRequestResponseResult.Rejected;
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

    private async Task<FriendRequestResult?> ValidateFriendRequestAsync(
        int requesterId,
        int recipientUserId,
        CancellationToken cancellationToken)
    {
        if (requesterId == recipientUserId)
        {
            return FriendRequestResult.CannotRequestSelf;
        }

        bool recipientExists = await _dbContext.Users
            .AnyAsync(user => user.UserId == recipientUserId
                && user.State == DatabaseValues.AccountStates.Active,
                cancellationToken);

        if (!recipientExists)
        {
            return FriendRequestResult.UserNotFound;
        }

        if (await AreFriendsAsync(
                requesterId,
                recipientUserId,
                cancellationToken))
        {
            return FriendRequestResult.AlreadyFriends;
        }

        if (await HasPendingRequestAsync(
                requesterId,
                recipientUserId,
                cancellationToken))
        {
            return FriendRequestResult.AlreadyPending;
        }

        return null;
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
        return _dbContext.FriendRequests.AnyAsync(request =>
            request.State == PendingRequestState
            && ((request.RequesterId == requesterId
                 && request.RecipientId == recipientUserId)
                || (request.RequesterId == recipientUserId
                    && request.RecipientId == requesterId)),
            cancellationToken);
    }
}
