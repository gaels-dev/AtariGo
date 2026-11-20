namespace AtariGo.Server.Application.Friends;

public sealed record SentFriendRequestsPage(
    IReadOnlyList<OutgoingFriendRequestEntry> Requests,
    long? NextRequestId);
