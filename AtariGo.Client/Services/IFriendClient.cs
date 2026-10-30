using AtariGo.Contracts;

namespace AtariGo.Client.Services;

public interface IFriendClient
{
    Task<SearchUsersResponse> SearchUsersAsync(
        string query,
        CancellationToken cancellationToken = default);

    Task<SendFriendRequestResponse> SendFriendRequestAsync(
        int recipientUserId,
        CancellationToken cancellationToken = default);

    Task<GetIncomingFriendRequestsResponse> GetIncomingFriendRequestsAsync(
        CancellationToken cancellationToken = default);

    Task<RespondToFriendRequestResponse> RespondToFriendRequestAsync(
        long requestId,
        bool accept,
        CancellationToken cancellationToken = default);
}
