using System.Threading.Channels;

using AtariGo.Contracts;

namespace AtariGo.Server.Application.Sessions;

public sealed class SessionConnectionLease : IDisposable
{
    private readonly CancellationTokenSource _connectionCancellation =
        new CancellationTokenSource();

    internal SessionConnectionLease(SessionConnectionInfo connectionInfo)
    {
        ConnectionInfo = connectionInfo;
        OutboundEvents = Channel.CreateBounded<SessionServerEvent>(
            new BoundedChannelOptions(SessionConnectionRegistry.MaximumQueuedEvents)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait
            });
    }

    public SessionConnectionInfo ConnectionInfo { get; internal set; }

    public Channel<SessionServerEvent> OutboundEvents { get; }

    public CancellationToken CancellationToken =>
        _connectionCancellation.Token;

    internal void Stop()
    {
        OutboundEvents.Writer.TryComplete();
        _connectionCancellation.Cancel();
    }

    public void Dispose()
    {
        _connectionCancellation.Dispose();
    }
}
