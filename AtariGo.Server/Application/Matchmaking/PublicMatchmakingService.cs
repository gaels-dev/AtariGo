using System.Runtime.CompilerServices;

using AtariGo.Contracts;
using AtariGo.Server.Application.Games;
using AtariGo.Server.Application.Rooms;

namespace AtariGo.Server.Application.Matchmaking;

public sealed class PublicMatchmakingService
{
    private static readonly TimeSpan minimumCountdownDelay = TimeSpan.FromMilliseconds(1);

    private readonly InMemoryPublicMatchmakingQueue _queue;
    private readonly IGameLifecycleService _gameLifecycle;
    private readonly TimeProvider _timeProvider;

    public PublicMatchmakingService(InMemoryPublicMatchmakingQueue queue,
        IGameLifecycleService gameLifecycle, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(gameLifecycle);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _queue = queue;
        _gameLifecycle = gameLifecycle;
        _timeProvider = timeProvider;
    }

    public MatchmakingTicket? Join(MatchmakingPlayer player)
    {
        ArgumentNullException.ThrowIfNull(player);
        return _queue.Join(player);
    }

    public async IAsyncEnumerable<MatchmakingEvent> ReadEventsAsync(MatchmakingTicket ticket,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        try
        {
            await foreach (MatchmakingEvent matchmakingEvent in
                ticket.Events.ReadAllAsync(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return matchmakingEvent;
                if (matchmakingEvent.EventCase
                    == MatchmakingEvent.EventOneofCase.MatchCountdownStarted)
                {
                    PublicMatchState? match = ticket.Match;
                    if (match is not null)
                    {
                        await CompleteCountdownAsync(match, cancellationToken);
                    }
                }
                if (matchmakingEvent.EventCase
                    == MatchmakingEvent.EventOneofCase.PublicGameStarted)
                {
                    yield break;
                }
            }
        }
        finally
        {
            _queue.Disconnect(ticket);
        }
    }

    private async Task CompleteCountdownAsync(PublicMatchState match,
        CancellationToken cancellationToken)
    {
        bool countdownCompleted = await WaitForCountdownAsync(match, cancellationToken);
        if (!countdownCompleted)
        {
            return;
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (_queue.TryClaimStart(match))
        {
            await PersistAndPublishGameAsync(match);
        }
        else
        {
            await match.GameCompletion.Task.WaitAsync(cancellationToken);
        }
    }

    private async Task<bool> WaitForCountdownAsync(PublicMatchState match,
        CancellationToken cancellationToken)
    {
        TimeSpan remaining = match.CountdownEndsAt - _timeProvider.GetUtcNow();
        if (remaining <= TimeSpan.Zero)
        {
            return true;
        }
        using CancellationTokenSource delayCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(
                match.CountdownCancellation.Token, cancellationToken);
        bool completed = true;
        try
        {
            while (remaining > TimeSpan.Zero)
            {
                TimeSpan delay = remaining < minimumCountdownDelay
                    ? minimumCountdownDelay : remaining;
                await Task.Delay(delay, _timeProvider, delayCancellation.Token);
                remaining = match.CountdownEndsAt - _timeProvider.GetUtcNow();
            }
        }
        catch (OperationCanceledException)
            when (match.CountdownCancellation.IsCancellationRequested)
        {
            completed = false;
        }
        return completed;
    }

    private async Task PersistAndPublishGameAsync(PublicMatchState match)
    {
        bool isComplete = false;
        try
        {
            PublicMatchStartRequest request = new PublicMatchStartRequest(
                match.BlackTicket.Player,
                match.WhiteTicket.Player,
                CreateGameConfiguration());
            long gameId = await _gameLifecycle.StartPublicGameAsync(
                request, CancellationToken.None);
            _queue.CompleteStart(match, gameId);
            isComplete = true;
        }
        finally
        {
            if (!isComplete)
            {
                _queue.FailStart(match);
            }
        }
    }

    private static LobbyConfiguration CreateGameConfiguration()
    {
        return new LobbyConfiguration(
            PublicGameRules.CaptureGoal,
            PublicGameRules.GameTimeSeconds,
            PublicGameRules.TurnTimeSeconds);
    }
}
