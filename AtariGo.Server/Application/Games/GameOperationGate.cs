using AtariGo.Server.Concurrency;

namespace AtariGo.Server.Application.Games;

public sealed class GameOperationGate : AsyncOperationGate
{
    private const int GateCount = 128;

    public GameOperationGate() : base(GateCount)
    {
    }

    public Task<AsyncOperationLease> EnterAsync(long gameId, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(gameId);
        return AcquireAsync(unchecked((int)gameId), cancellationToken);
    }
}
