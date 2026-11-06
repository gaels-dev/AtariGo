using System.Threading;

using Grpc.Core;

using AtariGo.Contracts;

namespace AtariGo.Server.Services;

public sealed class GameSessionStreamContext
{
    public GameSessionStreamContext(
        IAsyncStreamReader<GameSessionClientEvent> requestStream,
        IServerStreamWriter<GameSessionServerEvent> responseStream,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requestStream);
        ArgumentNullException.ThrowIfNull(responseStream);
        RequestStream = requestStream;
        ResponseStream = responseStream;
        CancellationToken = cancellationToken;
    }

    public IAsyncStreamReader<GameSessionClientEvent> RequestStream { get; }

    public IServerStreamWriter<GameSessionServerEvent> ResponseStream { get; }

    public CancellationToken CancellationToken { get; }
}
