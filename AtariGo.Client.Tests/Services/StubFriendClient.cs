using System.Threading;
using System.Threading.Tasks;

using AtariGo.Client.Services;
using AtariGo.Contracts;

namespace AtariGo.Client.Tests.Services;

public sealed class StubFriendClient : IFriendClient
{
    public Task<SearchUsersResponse> SearchResponse { get; set; } =
        Task.FromResult(new SearchUsersResponse());

    public Task<GetIncomingFriendRequestsResponse> IncomingResponse { get; set; } =
        Task.FromResult(new GetIncomingFriendRequestsResponse());

    public CancellationToken RequestToken { get; private set; }

    public Task<SearchUsersResponse> SearchUsersAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        RequestToken = cancellationToken;
        return SearchResponse;
    }

    public Task<SendFriendRequestResponse> SendFriendRequestAsync(
        int recipientUserId,
        CancellationToken cancellationToken = default)
    {
        RequestToken = cancellationToken;
        return Task.FromResult(new SendFriendRequestResponse());
    }

    public Task<GetIncomingFriendRequestsResponse> GetIncomingFriendRequestsAsync(
        CancellationToken cancellationToken = default)
    {
        RequestToken = cancellationToken;
        return IncomingResponse;
    }

    public Task<RespondToFriendRequestResponse> RespondToFriendRequestAsync(
        long requestId,
        bool accept,
        CancellationToken cancellationToken = default)
    {
        RequestToken = cancellationToken;
        return Task.FromResult(new RespondToFriendRequestResponse());
    }
}
