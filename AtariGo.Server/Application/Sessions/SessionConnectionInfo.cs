namespace AtariGo.Server.Application.Sessions;

public sealed record SessionConnectionInfo(
    long SessionId,
    int UserId,
    string UserName,
    IReadOnlyList<FriendPresenceEntry> Friends);
