using AtariGo.Client.Services;

namespace AtariGo.Client.Tests.Services;

internal sealed class StubGameSessionClient : IGameSessionClient
{
    public Queue<StubGameSessionStream> Streams { get; } = new();

    public Task<IGameSessionStream> ConnectAsync(long gameId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IGameSessionStream>(Streams.Dequeue());
    }
}
