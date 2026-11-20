using AtariGo.Server.Concurrency;

namespace AtariGo.Server.Application.Social;

public sealed class FriendMutationCoordinator : AsyncOperationGate
{
    private const int GateCount = 1;

    public FriendMutationCoordinator() : base(GateCount)
    {
    }

    public Task<AsyncOperationLease> EnterAsync(CancellationToken cancellationToken)
    {
        return AcquireAsync(0, cancellationToken);
    }
}
