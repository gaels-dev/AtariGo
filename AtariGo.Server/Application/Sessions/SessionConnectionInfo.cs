namespace AtariGo.Server.Application.Sessions;

public sealed record SessionConnectionInfo(
    long SessionId,
    int UserId,
    string UserName)
{
    public IReadOnlyList<FriendPresenceEntry> Friends
    {
        get; init;
    } =
        Array.Empty<FriendPresenceEntry>();
}
