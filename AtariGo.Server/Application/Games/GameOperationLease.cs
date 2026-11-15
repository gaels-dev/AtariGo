namespace AtariGo.Server.Application.Games;

public sealed class GameOperationLease : IDisposable
{
    private readonly SemaphoreSlim _gate;
    private GameOperationGate? _owner;

    internal GameOperationLease(GameOperationGate owner, SemaphoreSlim gate)
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
