using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using Grpc.Core;

using AtariGo.Contracts;

namespace AtariGo.Client.Services;

public sealed class GrpcGameSessionStream : IGameSessionStream
{
    private readonly AsyncDuplexStreamingCall<GameSessionClientEvent,
        GameSessionServerEvent> _call;

    public GrpcGameSessionStream(
        AsyncDuplexStreamingCall<GameSessionClientEvent, GameSessionServerEvent> call)
    {
        ArgumentNullException.ThrowIfNull(call);
        _call = call;
    }

    public async IAsyncEnumerable<GameSessionServerEvent> ReadEventsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (await _call.ResponseStream.MoveNext(cancellationToken))
        {
            yield return _call.ResponseStream.Current;
        }
    }

    public Task SendChatMessageAsync(
        string text,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);
        cancellationToken.ThrowIfCancellationRequested();
        GameSessionClientEvent clientEvent = new GameSessionClientEvent
        {
            SendChatMessage = new SendChatMessage { Text = text }
        };
        return _call.RequestStream.WriteAsync(clientEvent);
    }

    public ValueTask DisposeAsync()
    {
        _call.Dispose();
        return ValueTask.CompletedTask;
    }
}
