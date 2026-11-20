using AtariGo.Server.Application.Sessions;

namespace AtariGo.Server.Tests.Rooms;

internal sealed class TestRoomSessionService : ISessionService
{
    public Task<SessionIdentity?> ValidateSessionAsync(
        string sessionToken,
        CancellationToken cancellationToken)
    {
        SessionIdentity? identity = sessionToken switch
        {
            "host" => new SessionIdentity(1, "Host"),
            "guest" => new SessionIdentity(2, "Guest"),
            "other" => new SessionIdentity(3, "Other"),
            _ => null
        };

        return Task.FromResult(identity);
    }

    public Task<SessionConnectionInfo?> OpenConnectionAsync(
        string sessionToken,
        CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public Task CloseConnectionAsync(long sessionId, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public Task LogoutAsync(string sessionToken, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }
}
