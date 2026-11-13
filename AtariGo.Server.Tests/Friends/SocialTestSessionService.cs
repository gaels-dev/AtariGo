using Microsoft.EntityFrameworkCore;

using AtariGo.Server.Application.Sessions;
using AtariGo.Server.Data;
using AtariGo.Server.Tests.Rooms;

namespace AtariGo.Server.Tests.Friends;

internal sealed class SocialTestSessionService : ISessionService
{
    private readonly AtariGoDbContext _context;

    public SocialTestSessionService(AtariGoDbContext context)
    {
        _context = context;
    }

    public Task<SessionIdentity?> ValidateSessionAsync(
        string sessionToken,
        CancellationToken cancellationToken)
    {
        return new TestRoomSessionService().ValidateSessionAsync(sessionToken, cancellationToken);
    }

    public async Task<SessionConnectionInfo?> OpenConnectionAsync(
        string sessionToken,
        CancellationToken cancellationToken)
    {
        SessionIdentity? identity = await ValidateSessionAsync(sessionToken, cancellationToken);
        if (identity is null)
        {
            return null;
        }

        List<int> friendIds = await _context.Friendships
            .Where(pair => pair.UserId == identity.UserId || pair.FriendUserId == identity.UserId)
            .Select(pair => pair.UserId == identity.UserId ? pair.FriendUserId : pair.UserId)
            .ToListAsync(cancellationToken);
        List<FriendPresenceEntry> friends = await _context.Users
            .Where(user => friendIds.Contains(user.UserId))
            .Select(user => new FriendPresenceEntry(user.UserId, user.UserName, false))
            .ToListAsync(cancellationToken);
        return new SessionConnectionInfo(
            identity.UserId, identity.UserId, identity.UserName, friends);
    }

    public Task CloseConnectionAsync(long sessionId, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task LogoutAsync(string sessionToken, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
