using Grpc.Core;

using AtariGo.Contracts;

namespace AtariGo.Server.Tests.Games;

internal sealed class FailingGameSessionTransport : IAsyncStreamReader<GameSessionClientEvent>,
    IServerStreamWriter<GameSessionServerEvent>
{
    private readonly Exception _readFailure;
    private bool _joined;

    public FailingGameSessionTransport(long gameId, Exception readFailure)
    {
        Current = new GameSessionClientEvent { JoinGame = new JoinGame { GameId = gameId } };
        _readFailure = readFailure;
    }

    public GameSessionClientEvent Current { get; }

    public WriteOptions? WriteOptions { get; set; }

    public Exception? WriteFailure { get; init; }

    public Task<bool> MoveNext(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_joined)
        {
            throw _readFailure;
        }
        _joined = true;
        return Task.FromResult(true);
    }

    public Task WriteAsync(GameSessionServerEvent message)
    {
        if (WriteFailure is not null)
        {
            throw WriteFailure;
        }
        return Task.CompletedTask;
    }
}
