using AtariGo.Client.Services;

namespace AtariGo.Client.Tests.Services;

internal sealed class StubGameSessionClient : IGameSessionClient
{
    public Queue<StubGameSessionStream> Streams { get; } = new();

    public int ConnectCount { get; private set; }

    public Exception? ConnectFailure { get; set; }

    public Task<IGameSessionStream> ConnectAsync(long gameId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ConnectCount++;
        if (ConnectFailure is not null)
        {
            throw ConnectFailure;
        }
        return Task.FromResult<IGameSessionStream>(Streams.Dequeue());
    }
}
