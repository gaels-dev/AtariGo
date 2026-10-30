namespace AtariGo.Server.Application.Sessions;

public interface ISessionService
{
    Task<IReadOnlyList<FriendPresenceEntry>?> HeartbeatAsync(
        string sessionToken,
        CancellationToken cancellationToken);

    Task LogoutAsync(
        string sessionToken,
        CancellationToken cancellationToken);
}
