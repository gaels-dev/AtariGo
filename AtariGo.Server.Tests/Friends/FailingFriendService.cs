using AtariGo.Server.Application.Friends;

namespace AtariGo.Server.Tests.Friends;

internal sealed class FailingFriendService : IFriendService
{
    private readonly Func<Task> _failOperation;

    public FailingFriendService(Func<Task> failOperation)
    {
        _failOperation = failOperation;
    }

    public int InvocationCount { get; private set; }

    public async Task<IReadOnlyList<FriendCandidateEntry>?> SearchUsersAsync(
        string sessionToken, string query, CancellationToken cancellationToken)
    {
        await FailAsync();
        return null;
    }

    public async Task<SendFriendRequestResult?> SendFriendRequestAsync(
        string sessionToken, int recipientUserId, CancellationToken cancellationToken)
    {
        await FailAsync();
        return null;
    }

    public async Task<IReadOnlyList<IncomingFriendRequestEntry>?> GetIncomingFriendRequestsAsync(
        string sessionToken, CancellationToken cancellationToken)
    {
        await FailAsync();
        return null;
    }

    public async Task<FriendRequestResponseResult?> RespondToFriendRequestAsync(
        RespondToFriendRequestCommand command, CancellationToken cancellationToken)
    {
        await FailAsync();
        return null;
    }

    public async Task<SentFriendRequestsPage?> ListFriendRequestsBySenderAsync(
        string sessionToken, SentFriendRequestsQuery query, CancellationToken cancellationToken)
    {
        await FailAsync();
        return null;
    }

    public async Task<CancelFriendRequestResult?> CancelFriendRequestAsync(
        string sessionToken, long requestId, CancellationToken cancellationToken)
    {
        await FailAsync();
        return null;
    }

    private async Task FailAsync()
    {
        InvocationCount++;
        await _failOperation();
    }
}
