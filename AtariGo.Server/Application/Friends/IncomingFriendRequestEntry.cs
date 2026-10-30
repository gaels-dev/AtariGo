namespace AtariGo.Server.Application.Friends;

public sealed record IncomingFriendRequestEntry(
    long RequestId,
    int RequesterUserId,
    string RequesterUserName);
