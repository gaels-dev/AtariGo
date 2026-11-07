namespace AtariGo.Server.Application.Friends;

public sealed class FriendMutationLease : IDisposable
{
    private SemaphoreSlim? _gate;

    internal FriendMutationLease(SemaphoreSlim gate)
    {
        _gate = gate;
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref _gate, null)?.Release();
    }
}
