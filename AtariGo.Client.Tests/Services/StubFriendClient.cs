
using AtariGo.Client.Services;
using AtariGo.Contracts;

namespace AtariGo.Client.Tests.Services;

public sealed class StubFriendClient : IFriendClient
{
    public Task<SearchUsersResponse> SearchResponse { get; set; } =
        Task.FromResult(new SearchUsersResponse());

    public Task<GetIncomingFriendRequestsResponse> IncomingResponse { get; set; } =
        Task.FromResult(new GetIncomingFriendRequestsResponse());

    public Task<ListFriendRequestsBySenderResponse> SentResponse { get; set; } =
        Task.FromResult(new ListFriendRequestsBySenderResponse());

    public Queue<ListFriendRequestsBySenderResponse> SentPages { get; } = new();

    public List<string> PageTokens { get; } = new();

    public Task<CancelFriendRequestResponse>? CancelResponse { get; set; }

    public FriendRequestResponseStatus ResponseStatus { get; set; }

    public Task<RespondToFriendRequestResponse>? Response { get; set; }

    public CancelFriendRequestStatus CancelStatus { get; set; } =
        CancelFriendRequestStatus.Cancelled;

    public CancellationToken RequestToken { get; private set; }

    public Task<ListFriendRequestsBySenderResponse> ListFriendRequestsBySenderAsync(
        string pageToken,
        CancellationToken cancellationToken = default)
    {
        RequestToken = cancellationToken;
        PageTokens.Add(pageToken);
        return SentPages.Count > 0 ? Task.FromResult(SentPages.Dequeue()) : SentResponse;
    }

    public Task<CancelFriendRequestResponse> CancelFriendRequestAsync(
        long requestId,
        CancellationToken cancellationToken = default)
    {
        RequestToken = cancellationToken;
        return CancelResponse
            ?? Task.FromResult(new CancelFriendRequestResponse { Status = CancelStatus });
    }


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
        return Response
            ?? Task.FromResult(new RespondToFriendRequestResponse { Status = ResponseStatus });
    }
}
