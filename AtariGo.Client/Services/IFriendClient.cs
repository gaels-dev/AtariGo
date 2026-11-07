using AtariGo.Contracts;

namespace AtariGo.Client.Services;

public interface IFriendClient
{
    Task<ListFriendRequestsBySenderResponse> ListFriendRequestsBySenderAsync(
        string pageToken,
        CancellationToken cancellationToken = default);

    Task<CancelFriendRequestResponse> CancelFriendRequestAsync(
        long requestId,
        CancellationToken cancellationToken = default);

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
