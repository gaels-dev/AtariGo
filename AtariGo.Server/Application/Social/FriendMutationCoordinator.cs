namespace AtariGo.Server.Application.Social;

public sealed class FriendMutationCoordinator : IDisposable, IAsyncDisposable
{
    private const int MaximumConcurrentMutations = 1;

    private readonly SemaphoreSlim _gate = new SemaphoreSlim(
        MaximumConcurrentMutations, MaximumConcurrentMutations);
    private readonly object _lifetimeSync = new object();
    private readonly TaskCompletionSource _disposalCompletion = new TaskCompletionSource(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private int _pendingOperations;
    private bool _isDisposed;

    public async Task<FriendMutationLease> EnterAsync(CancellationToken cancellationToken)
    {
        FriendMutationLease lease = new FriendMutationLease(this);
        lock (_lifetimeSync)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            _pendingOperations++;
        }
        try
        {
            await _gate.WaitAsync(cancellationToken);
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

    internal void Exit()
    {
        _gate.Release();
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
            _gate.Dispose();
            _disposalCompletion.SetResult();
        }
    }
}
