using AtariGo.Server.Application.Friendships;

namespace AtariGo.Server.Tests.Friends;

internal sealed class FailingFriendshipsService : IFriendshipsService
{
    private readonly Func<Task<RemoveFriendshipResult?>> _failOperation;

    public FailingFriendshipsService(Func<Task<RemoveFriendshipResult?>> failOperation)
    {
        _failOperation = failOperation;
    }

    public int InvocationCount { get; private set; }

    public Task<RemoveFriendshipResult?> RemoveFriendshipAsync(
        string sessionToken, int friendUserId, CancellationToken cancellationToken)
    {
        InvocationCount++;
        return _failOperation();
    }
}
