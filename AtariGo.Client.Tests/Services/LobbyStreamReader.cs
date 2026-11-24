using Grpc.Core;

using AtariGo.Contracts;

namespace AtariGo.Client.Tests.Services;

internal sealed class LobbyStreamReader : IAsyncStreamReader<MatchmakingEvent>
{
    private readonly Queue<MatchmakingEvent> _events;

    internal LobbyStreamReader(IEnumerable<MatchmakingEvent> events)
    {
        _events = new Queue<MatchmakingEvent>(events);
    }

    public MatchmakingEvent Current { get; private set; } = new MatchmakingEvent();

    public Task<bool> MoveNext(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        bool received = _events.TryDequeue(out MatchmakingEvent? next);
        if (received)
        {
            Current = next!;
        }
        return Task.FromResult(received);
    }
}
