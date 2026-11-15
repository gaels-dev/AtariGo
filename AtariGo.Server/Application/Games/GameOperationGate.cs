namespace AtariGo.Server.Application.Games;

public sealed class GameOperationGate : IDisposable, IAsyncDisposable
{
    private const int GateCount = 128;

    private readonly SemaphoreSlim[] _gates = Enumerable.Range(0, GateCount)
        .Select(_ => new SemaphoreSlim(1, 1)).ToArray();
    private readonly object _lifetimeSync = new object();
    private readonly TaskCompletionSource _disposalCompletion = new TaskCompletionSource(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private int _pendingOperations;
    private bool _isDisposed;

    public async Task<GameOperationLease> EnterAsync(long gameId,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(gameId);
        SemaphoreSlim gate = _gates[(int)((ulong)gameId % GateCount)];
        GameOperationLease lease = new GameOperationLease(this, gate);
        lock (_lifetimeSync)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            _pendingOperations++;
        }
        try
        {
            await gate.WaitAsync(cancellationToken);
        }
        catch
        {
            FinishOperation();
            throw;
        }
        return lease;
    }

    public void Dispose()
    {
        lock (_lifetimeSync)
        {
            _isDisposed = true;
            TryFinishDisposal();
        }
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return new ValueTask(_disposalCompletion.Task);
    }

    internal void Exit(SemaphoreSlim gate)
    {
        gate.Release();
        FinishOperation();
    }

    private void FinishOperation()
    {
        lock (_lifetimeSync)
        {
            _pendingOperations--;
            TryFinishDisposal();
        }
    }

    private void TryFinishDisposal()
    {
        if (_isDisposed && _pendingOperations == 0 && !_disposalCompletion.Task.IsCompleted)
        {
            foreach (SemaphoreSlim gate in _gates)
            {
                gate.Dispose();
            }
            _disposalCompletion.SetResult();
        }
    }
}
