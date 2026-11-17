using AtariGo.Server.Application.Games;
using AtariGo.Server.Application.Sessions;

namespace AtariGo.Server.Application.PasswordRecovery;

public sealed class RecoverySessionRevoker
{
    private readonly ISessionConnectionRegistry _sessions;
    private readonly IGameSessionRegistry _games;

    public RecoverySessionRevoker(ISessionConnectionRegistry sessions, IGameSessionRegistry games)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(games);
        _sessions = sessions;
        _games = games;
    }

    public void Disconnect(int userId, IReadOnlyList<long> sessionIds)
    {
        foreach (long sessionId in sessionIds)
        {
            _sessions.DisconnectSession(sessionId);
        }
        _games.DisconnectUser(userId);
    }
}
