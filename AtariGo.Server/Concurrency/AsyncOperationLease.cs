namespace AtariGo.Server.Concurrency;

public sealed class AsyncOperationLease : IDisposable
{
    private readonly SemaphoreSlim _gate;
    private AsyncOperationGate? _owner;

    internal AsyncOperationLease(AsyncOperationGate owner, SemaphoreSlim gate)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(gate);
        _owner = owner;
        _gate = gate;
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref _owner, null)?.Exit(_gate);
    }
}
