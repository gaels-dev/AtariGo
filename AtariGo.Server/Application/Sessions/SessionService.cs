using Microsoft.EntityFrameworkCore;

using AtariGo.Server.Data;
using AtariGo.Server.Data.Entities;
using AtariGo.Server.Security;

namespace AtariGo.Server.Application.Sessions;

public sealed class SessionService : ISessionService
{
    private static readonly TimeSpan s_sessionIdleLifetime =
        TimeSpan.FromHours(1);

    private readonly AtariGoDbContext _dbContext;
    private readonly ISessionTokenProvider _tokenProvider;
    private readonly ISessionConnectionRegistry _connectionRegistry;
    private readonly TimeProvider _timeProvider;

    public SessionService(
        AtariGoDbContext dbContext,
        ISessionTokenProvider tokenProvider,
        ISessionConnectionRegistry connectionRegistry,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(tokenProvider);
        ArgumentNullException.ThrowIfNull(connectionRegistry);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _dbContext = dbContext;
        _tokenProvider = tokenProvider;
        _connectionRegistry = connectionRegistry;
        _timeProvider = timeProvider;
    }

    public async Task<SessionConnectionInfo?> OpenConnectionAsync(
        string sessionToken,
        CancellationToken cancellationToken)
    {
        Session? session = await FindActiveSessionAsync(
            sessionToken,
            cancellationToken);
        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        if (session is null || !IsSessionValid(session, now))
        {
            return null;
        }

        ExtendSession(session, now);
        await _dbContext.SaveChangesAsync(cancellationToken);
        IReadOnlyList<FriendPresenceEntry> friends =
            await FindFriendsAsync(session.UserId, cancellationToken);

        return new SessionConnectionInfo(
            session.SessionId,
            session.UserId,
            session.User.UserName,
            friends);
    }

    public async Task CloseConnectionAsync(
        long sessionId,
        CancellationToken cancellationToken)
    {
        Session? session = await _dbContext.Sessions
            .SingleOrDefaultAsync(
                candidate => candidate.SessionId == sessionId
                    && candidate.State == DatabaseValues.SessionStates.Active,
                cancellationToken);
        if (session is null)
        {
            return;
        }

        ExtendSession(session, _timeProvider.GetUtcNow().UtcDateTime);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task LogoutAsync(
        string sessionToken,
        CancellationToken cancellationToken)
    {
        Session? session = await FindSessionByTokenAsync(
            sessionToken,
            cancellationToken);
        if (session is null)
        {
            return;
        }

        session.State = DatabaseValues.SessionStates.Revoked;
        session.LastActivityAt = _timeProvider.GetUtcNow().UtcDateTime;
        await _dbContext.SaveChangesAsync(cancellationToken);
        _connectionRegistry.DisconnectSession(session.SessionId);
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

    private Task<Session?> FindSessionByTokenAsync(
        string sessionToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sessionToken))
        {
            return Task.FromResult<Session?>(null);
        }

        string tokenHash = _tokenProvider.Hash(sessionToken);
        return _dbContext.Sessions.SingleOrDefaultAsync(
            candidate => candidate.SessionTokenHash == tokenHash
                && candidate.State == DatabaseValues.SessionStates.Active,
            cancellationToken);
    }

    private bool IsSessionValid(Session session, DateTime now)
    {
        bool isWithinIdleLifetime = session.ExpiresAt > now
            || _connectionRegistry.IsSessionConnected(session.SessionId);
        return isWithinIdleLifetime
            && session.User.State == DatabaseValues.AccountStates.Active;
    }

    private void ExtendSession(Session session, DateTime now)
    {
        session.LastActivityAt = now;
        session.ExpiresAt = now.Add(s_sessionIdleLifetime);
    }

    private async Task<IReadOnlyList<FriendPresenceEntry>> FindFriendsAsync(
        int userId,
        CancellationToken cancellationToken)
    {
        List<int> friendIds = await FindFriendIdsAsync(
            userId,
            cancellationToken);
        return await _dbContext.Users
            .AsNoTracking()
            .Where(friend => friendIds.Contains(friend.UserId)
                && friend.State == DatabaseValues.AccountStates.Active)
            .OrderBy(friend => friend.UserName)
            .Select(friend => new FriendPresenceEntry(
                friend.UserId,
                friend.UserName,
                false))
            .ToListAsync(cancellationToken);
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
