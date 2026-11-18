namespace AtariGo.Server.Application.Authentication;

public sealed class PendingLoginLease : IDisposable
{
    private readonly int _gateIndex;
    private PendingLoginStore? _owner;

    internal PendingLoginLease(PendingLoginStore owner, int gateIndex)
    {
        _owner = owner;
        _gateIndex = gateIndex;
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref _owner, null)?.Exit(_gateIndex);
    }
}
