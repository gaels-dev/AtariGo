namespace AtariGo.Server.Concurrency;

public class AsyncOperationGate : IDisposable, IAsyncDisposable
{
    private readonly SemaphoreSlim[] _gates;
    private readonly object _lifetimeSync = new object();
    private readonly TaskCompletionSource _disposalCompletion = new TaskCompletionSource(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private int _pendingOperations;
    private bool _isDisposed;

    public AsyncOperationGate(int gateCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(gateCount);
        _gates = Enumerable.Range(0, gateCount).Select(_ => new SemaphoreSlim(1, 1)).ToArray();
    }

    public async Task<AsyncOperationLease> AcquireAsync(int key,
        CancellationToken cancellationToken)
    {
        SemaphoreSlim gate = _gates[(int)((uint)key % _gates.Length)];
        var lease = new AsyncOperationLease(this, gate);
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
