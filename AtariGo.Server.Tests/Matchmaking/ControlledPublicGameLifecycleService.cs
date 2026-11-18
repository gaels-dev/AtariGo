using AtariGo.Server.Application.Games;
using AtariGo.Server.Application.Matchmaking;
using AtariGo.Server.Application.Rooms;

namespace AtariGo.Server.Tests.Matchmaking;

internal sealed class ControlledPublicGameLifecycleService : IGameLifecycleService
{
    private int _creationCount;

    public int CreationCount => _creationCount;

    public TaskCompletionSource Started
    {
        get;
    } = new TaskCompletionSource(
        TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource<long> Completion
    {
        get;
    } = new TaskCompletionSource<long>(
        TaskCreationOptions.RunContinuationsAsynchronously);

    public PublicMatchStartRequest? Request
    {
        get; private set;
    }

    public CancellationToken CreationCancellation
    {
        get; private set;
    }

    public Task<long> StartPublicGameAsync(PublicMatchStartRequest request,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _creationCount);
        Request = request;
        CreationCancellation = cancellationToken;
        Started.TrySetResult();
        return Completion.Task;
    }

    public Task<RoomOperationResult> StartGameAsync(RoomStartRequest startRequest,
        CancellationToken cancellationToken)
    {
        throw new NotSupportedException("This test double only creates public games.");
    }
}
