namespace AtariGo.Server.Application.Social;

public sealed class FriendMutationLease : IDisposable
{
    private FriendMutationCoordinator? _owner;

    internal FriendMutationLease(FriendMutationCoordinator owner)
    {
        _owner = owner;
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref _owner, null)?.Exit();
    }
}
