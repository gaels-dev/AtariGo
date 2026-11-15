namespace AtariGo.Client.Tests.Services;

internal sealed class RecordingSynchronizationContext : SynchronizationContext
{
    private int _sendCount;

    public int SendCount => _sendCount;

    public override void Send(SendOrPostCallback callback, object? state)
    {
        Interlocked.Increment(ref _sendCount);
        base.Send(callback, state);
    }
}
