namespace AtariGo.Server.Application.Friends;

public interface IFriendService
{
    Task<IReadOnlyList<FriendAccountEntry>?> SearchUsersAsync(
        string sessionToken,
        string query,
        CancellationToken cancellationToken);

    Task<FriendRequestResult?> SendFriendRequestAsync(
        string sessionToken,
        int recipientUserId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<IncomingFriendRequestEntry>?>
        GetIncomingFriendRequestsAsync(
            string sessionToken,
            CancellationToken cancellationToken);

    Task<FriendRequestResponseResult?> RespondToFriendRequestAsync(
        string sessionToken,
        long requestId,
        bool accept,
        CancellationToken cancellationToken);
}
