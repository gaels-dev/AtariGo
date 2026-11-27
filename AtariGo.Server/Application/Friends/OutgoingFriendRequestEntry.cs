namespace AtariGo.Server.Application.Friends;

public sealed record OutgoingFriendRequestEntry(
    long RequestId,
    int RecipientUserId,
    string RecipientUserName);
