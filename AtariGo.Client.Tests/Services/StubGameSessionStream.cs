using System.Collections.Concurrent;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

using AtariGo.Client.Services;
using AtariGo.Contracts;

namespace AtariGo.Client.Tests.Services;

internal sealed class StubGameSessionStream : IGameSessionStream
{
    private readonly Channel<GameSessionServerEvent> _events =
        Channel.CreateUnbounded<GameSessionServerEvent>();

    public ConcurrentQueue<GameSessionClientEvent> Sent { get; } = new();
    public bool FailSend { get; set; }

    public Exception? SendFailure { get; set; }

    public bool IsDisposed { get; private set; }

    public CancellationToken ReadCancellation { get; private set; }

    public void Publish(GameSessionServerEvent serverEvent)
    {
        _events.Writer.TryWrite(serverEvent);
    }

    public void Fail(Exception exception)
    {
        _events.Writer.TryComplete(exception);
    }

    public async IAsyncEnumerable<GameSessionServerEvent> ReadEventsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ReadCancellation = cancellationToken;
        await foreach (GameSessionServerEvent serverEvent in _events.Reader.ReadAllAsync(
            cancellationToken))
        {
            yield return serverEvent;
        }
    }

    public Task SendChatMessageAsync(string text, CancellationToken cancellationToken)
    {
        return SendEventAsync(new GameSessionClientEvent
        {
            SendChatMessage = new SendChatMessage { Text = text }
        }, cancellationToken);
    }

    public Task SendEventAsync(GameSessionClientEvent clientEvent,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Sent.Enqueue(clientEvent.Clone());
        if (SendFailure is not null)
        {
            throw SendFailure;
        }
        if (FailSend)
        {
            throw new IOException("Simulated lost confirmation.");
        }
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        _events.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }
}
