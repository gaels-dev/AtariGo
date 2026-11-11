using Microsoft.EntityFrameworkCore;

using AtariGo.Server.Application.Friends;
using AtariGo.Server.Application.Sessions;
using AtariGo.Server.Application.Social;
using AtariGo.Server.Data;

namespace AtariGo.Server.Application.Friendships;

public sealed class FriendshipsService : IFriendshipsService
{
    private readonly AtariGoDbContext _dbContext;
    private readonly ISessionService _sessionService;
    private readonly ISessionConnectionRegistry _connections;
    private readonly FriendMutationCoordinator _mutations;
    private readonly ILogger<FriendshipsService> _logger;

    public FriendshipsService(
        AtariGoDbContext dbContext,
        ISessionService sessionService,
        ISessionConnectionRegistry connections,
        FriendMutationCoordinator mutations,
        ILogger<FriendshipsService> logger)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(sessionService);
        ArgumentNullException.ThrowIfNull(connections);
        ArgumentNullException.ThrowIfNull(mutations);
        ArgumentNullException.ThrowIfNull(logger);
        _dbContext = dbContext;
        _sessionService = sessionService;
        _connections = connections;
        _mutations = mutations;
        _logger = logger;
    }

    public async Task<RemoveFriendshipResult?> RemoveFriendshipAsync(
        string sessionToken,
        int friendUserId,
        CancellationToken cancellationToken)
    {
        SessionIdentity? identity = await _sessionService.ValidateSessionAsync(
            sessionToken, cancellationToken);
        if (identity is null)
        {
            return null;
        }

        if (friendUserId <= 0 || friendUserId == identity.UserId)
        {
            throw new ArgumentException("Invalid friend identifier.", nameof(friendUserId));
        }

        using FriendMutationLease lease = await _mutations.EnterAsync(cancellationToken);
        int firstUserId = Math.Min(identity.UserId, friendUserId);
        int secondUserId = Math.Max(identity.UserId, friendUserId);
        int removed = await _dbContext.Friendships
            .Where(friendship => friendship.UserId == firstUserId
                && friendship.FriendUserId == secondUserId)
            .ExecuteDeleteAsync(cancellationToken);
        _connections.RemoveFriendship(identity.UserId, friendUserId);
        _logger.LogInformation(
            "Friendship removal for users {UserId} and {FriendUserId}: {Removed}.",
            identity.UserId, friendUserId, removed > 0);
        return removed > 0
            ? RemoveFriendshipResult.Removed
            : RemoveFriendshipResult.AlreadyAbsent;
    }
}
