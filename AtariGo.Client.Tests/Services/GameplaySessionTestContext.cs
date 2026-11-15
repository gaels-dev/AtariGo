using System.Collections.Concurrent;

using AtariGo.Client.Models;
using AtariGo.Client.Services;
using AtariGo.Contracts;

namespace AtariGo.Client.Tests.Services;

internal sealed class GameplaySessionTestContext : IAsyncDisposable
{
    private readonly StubGameSessionStream _first;
    private Task _runTask = Task.CompletedTask;

    public GameplaySessionTestContext(StubGameSessionStream first,
        StubGameSessionStream? second = null)
    {
        _first = first;
        Client.Streams.Enqueue(first);
        if (second is not null)
        {
            Client.Streams.Enqueue(second);
        }
        Session = new GameplaySession(Client);
        Session.Updated += Updates.Enqueue;
    }

    public StubGameSessionClient Client { get; } = new StubGameSessionClient();

    public GameplaySession Session { get; }

    public ConcurrentQueue<GameplaySessionUpdate> Updates { get; } =
        new ConcurrentQueue<GameplaySessionUpdate>();

    public async Task StartAsync(long version = 1)
    {
        _runTask = Session.RunAsync(1, CancellationToken.None);
        _first.Publish(new GameSessionServerEvent
        {
            Snapshot = GameSessionTestData.Snapshot(version)
        });
        await WaitUntilAsync(() => Updates.Any(update => update.ServerEvent?.Snapshot is not null));
    }

    public static async Task WaitUntilAsync(Func<bool> condition)
    {
        using CancellationTokenSource timeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(5));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }

    public async ValueTask DisposeAsync()
    {
        Session.Dispose();
        await _runTask.WaitAsync(TimeSpan.FromSeconds(5));
    }
}
