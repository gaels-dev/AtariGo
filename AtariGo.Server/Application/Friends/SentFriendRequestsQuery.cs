namespace AtariGo.Server.Application.Friends;

public sealed record SentFriendRequestsQuery(int PageSize, long AfterRequestId);
