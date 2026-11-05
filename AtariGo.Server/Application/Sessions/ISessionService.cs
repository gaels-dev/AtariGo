namespace AtariGo.Server.Application.Sessions;

public interface ISessionService
{
    Task<SessionConnectionInfo?> OpenConnectionAsync(
        string sessionToken,
        CancellationToken cancellationToken);

    Task CloseConnectionAsync(
        long sessionId,
        CancellationToken cancellationToken);

    Task LogoutAsync(
        string sessionToken,
        CancellationToken cancellationToken);
}
