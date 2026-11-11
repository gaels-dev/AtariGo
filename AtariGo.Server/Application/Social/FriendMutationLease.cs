namespace AtariGo.Server.Application.Social;

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
