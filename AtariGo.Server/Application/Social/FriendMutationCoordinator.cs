namespace AtariGo.Server.Application.Social;

public sealed class FriendMutationCoordinator : IDisposable
{
    private const int MaximumConcurrentMutations = 1;

    private readonly SemaphoreSlim _gate = new SemaphoreSlim(
        MaximumConcurrentMutations, MaximumConcurrentMutations);

    public async Task<FriendMutationLease> EnterAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        return new FriendMutationLease(_gate);
    }

    public void Dispose()
    {
        _gate.Dispose();
    }
}
