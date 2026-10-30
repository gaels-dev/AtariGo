using AtariGo.Server.Data;
using AtariGo.Server.Data.Entities;
using AtariGo.Server.Security;
using Microsoft.EntityFrameworkCore;

namespace AtariGo.Server.Application.Sessions;

public sealed class SessionService : ISessionService
{
    private static readonly TimeSpan SessionIdleLifetime =
        TimeSpan.FromHours(1);
    private static readonly TimeSpan PresenceWindow =
        TimeSpan.FromSeconds(90);

    private readonly AtariGoDbContext _dbContext;
    private readonly ISessionTokenProvider _tokenProvider;
    private readonly TimeProvider _timeProvider;

    public SessionService(
        AtariGoDbContext dbContext,
        ISessionTokenProvider tokenProvider,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(tokenProvider);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _dbContext = dbContext;
        _tokenProvider = tokenProvider;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<FriendPresenceEntry>?> HeartbeatAsync(
        string sessionToken,
        CancellationToken cancellationToken)
    {
        Session? session = await FindActiveSessionAsync(
            sessionToken,
            cancellationToken);
        if (session is null)
        {
            return null;
        }

        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        if (!IsSessionValid(session, now))
        {
            await ExpireSessionAsync(session, cancellationToken);
            return null;
        }

        ExtendSession(session, now);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await FindFriendPresenceAsync(
            session.UserId,
            now,
            cancellationToken);
    }

    public async Task LogoutAsync(
        string sessionToken,
        CancellationToken cancellationToken)
    {
        string tokenHash = _tokenProvider.Hash(sessionToken);
        Session? session = await _dbContext.Sessions
            .SingleOrDefaultAsync(
                candidate => candidate.SessionTokenHash == tokenHash
                    && candidate.State == DatabaseValues.SessionStates.Active,
                cancellationToken);

        if (session is null)
        {
            return;
        }

        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        session.State = session.ExpiresAt <= now
            ? DatabaseValues.SessionStates.Expired
            : DatabaseValues.SessionStates.Revoked;
        session.LastActivityAt = now;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private Task<Session?> FindActiveSessionAsync(
        string sessionToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sessionToken))
        {
            return Task.FromResult<Session?>(null);
        }

        string tokenHash = _tokenProvider.Hash(sessionToken);

        return _dbContext.Sessions
            .Include(candidate => candidate.User)
            .SingleOrDefaultAsync(
                candidate => candidate.SessionTokenHash == tokenHash
                    && candidate.State == DatabaseValues.SessionStates.Active,
                cancellationToken);
    }

    private static bool IsSessionValid(Session session, DateTime now)
    {
        return session.ExpiresAt > now
            && session.User.State == DatabaseValues.AccountStates.Active;
    }

    private async Task ExpireSessionAsync(
        Session session,
        CancellationToken cancellationToken)
    {
        session.State = DatabaseValues.SessionStates.Expired;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void ExtendSession(Session session, DateTime now)
    {
        session.LastActivityAt = now;
        session.ExpiresAt = now.Add(SessionIdleLifetime);
    }

    private async Task<IReadOnlyList<FriendPresenceEntry>> FindFriendPresenceAsync(
        int userId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        List<int> friendIds = await FindFriendIdsAsync(
            userId,
            cancellationToken);
        DateTime presenceCutoff = now.Subtract(PresenceWindow);

        var friendStates = await _dbContext.Users
            .AsNoTracking()
            .Where(friend => friendIds.Contains(friend.UserId)
                && friend.State == DatabaseValues.AccountStates.Active)
            .OrderBy(friend => friend.UserName)
            .Select(friend => new
            {
                friend.UserId,
                friend.UserName,
                IsOnline = friend.Sessions.Any(friendSession =>
                    friendSession.State == DatabaseValues.SessionStates.Active
                    && friendSession.ExpiresAt > now
                    && friendSession.LastActivityAt >= presenceCutoff)
            })
            .ToListAsync(cancellationToken);

        return friendStates
            .Select(friend => new FriendPresenceEntry(
                friend.UserId,
                friend.UserName,
                friend.IsOnline))
            .ToList();
    }

    private Task<List<int>> FindFriendIdsAsync(
        int userId,
        CancellationToken cancellationToken)
    {
        return _dbContext.Friendships
            .AsNoTracking()
            .Where(friendship => friendship.UserId == userId
                || friendship.FriendUserId == userId)
            .Select(friendship => friendship.UserId == userId
                ? friendship.FriendUserId
                : friendship.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }
}
