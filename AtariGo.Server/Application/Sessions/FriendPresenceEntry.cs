namespace AtariGo.Server.Application.Sessions;

public sealed record FriendPresenceEntry(
    int UserId,
    string UserName,
    bool IsOnline);
