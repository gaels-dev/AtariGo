using AtariGo.Server.Application.Games;
using AtariGo.Server.Application.Sessions;
using AtariGo.Diagnostics;
using AtariGo.Server.Diagnostics;

namespace AtariGo.Server.Application.PasswordRecovery;

public sealed class RecoverySessionRevoker
{
    private readonly ISessionConnectionRegistry _sessions;
    private readonly IGameSessionRegistry _games;
    private readonly ILogger<RecoverySessionRevoker> _logger;

    public RecoverySessionRevoker(ISessionConnectionRegistry sessions, IGameSessionRegistry games,
        ILogger<RecoverySessionRevoker> logger)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(games);
        ArgumentNullException.ThrowIfNull(logger);
        _sessions = sessions;
        _games = games;
        _logger = logger;
    }

    public void Disconnect(int userId, IReadOnlyList<long> sessionIds)
    {
        ArgumentNullException.ThrowIfNull(sessionIds);
        foreach (long sessionId in sessionIds)
        {
            _sessions.DisconnectSession(sessionId);
        }
        _games.DisconnectUser(userId);
        ServerIdentityDiagnostics.Record(_logger, IdentityOperation.RevokeSessions,
            new DiagnosticEventDetails
            {
                Outcome = DiagnosticOutcome.Succeeded,
                Context = new DiagnosticContext { UserId = userId > 0 ? userId : null }
            });
    }
}
