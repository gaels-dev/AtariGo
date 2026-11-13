namespace AtariGo.Server.Application.Friends;

public sealed record RespondToFriendRequestCommand(
    string SessionToken,
    long RequestId,
    bool Accept);
