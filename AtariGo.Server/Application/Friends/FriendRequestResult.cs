namespace AtariGo.Server.Application.Friends;

public enum FriendRequestResult
{
    Sent,
    AlreadyFriends,
    AlreadyPending,
    UserNotFound,
    CannotRequestSelf
}
