namespace AtariGo.Server.Application.Friends;

public enum SendFriendRequestResult
{
    Sent,
    AlreadyFriends,
    AlreadyPending,
    UserNotFound,
    CannotRequestSelf
}
