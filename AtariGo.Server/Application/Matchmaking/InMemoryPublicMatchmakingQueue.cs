using AtariGo.Contracts;
using AtariGo.Server.Data;

namespace AtariGo.Server.Application.Matchmaking;

public sealed class InMemoryPublicMatchmakingQueue : IPublicMatchmakingQueue
{
    private static readonly TimeSpan s_countdownDuration =
        TimeSpan.FromSeconds(PublicGameRules.CountdownSeconds);

    private readonly object _sync = new object();
    private readonly LinkedList<MatchmakingTicket> _waiting = new LinkedList<MatchmakingTicket>();
    private readonly Dictionary<string, MatchmakingTicket> _activeTickets =
        new Dictionary<string, MatchmakingTicket>(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider;

    public InMemoryPublicMatchmakingQueue(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
    }

    public MatchmakingTicket? Join(MatchmakingPlayer player)
    {
        ArgumentNullException.ThrowIfNull(player);
        lock (_sync)
        {
            if (_activeTickets.ContainsKey(player.PlayerKey))
            {
                return null;
            }

            MatchmakingTicket ticket = new MatchmakingTicket(player);
            ticket.QueueNode = _waiting.AddLast(ticket);
            _activeTickets.Add(player.PlayerKey, ticket);
            ticket.Notifications.Writer.TryWrite(new MatchmakingEvent
            {
                SearchQueued = new SearchQueued()
            });
            PairWaitingPlayers();
            return ticket;
        }
    }

    public void Disconnect(MatchmakingTicket ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        lock (_sync)
        {
            if (ticket.QueueNode is not null)
            {
                _waiting.Remove(ticket.QueueNode);
                ticket.QueueNode = null;
                _activeTickets.Remove(ticket.Player.PlayerKey);
                ticket.Notifications.Writer.TryComplete();
                return;
            }

            if (ticket.Match is not null && !ticket.Match.IsStarting
                && !ticket.Match.IsCompleted)
            {
                CancelCountdown(ticket.Match, ticket);
                return;
            }

            ticket.Notifications.Writer.TryComplete();
        }
    }

    public bool TryClaimStart(PublicMatchState match)
    {
        ArgumentNullException.ThrowIfNull(match);
        lock (_sync)
        {
            if (match.IsStarting || match.IsCompleted
                || _timeProvider.GetUtcNow() < match.CountdownEndsAt)
            {
                return false;
            }

            match.IsStarting = true;
            return true;
        }
    }

    public void CompleteStart(PublicMatchState match, long gameId)
    {
        ArgumentNullException.ThrowIfNull(match);
        lock (_sync)
        {
            match.IsCompleted = true;
            match.GameCompletion.TrySetResult(gameId);
            PublishGameStarted(match, gameId);
            RemovePlayer(match.BlackTicket);
            RemovePlayer(match.WhiteTicket);
            match.BlackTicket.Notifications.Writer.TryComplete();
            match.WhiteTicket.Notifications.Writer.TryComplete();
        }
    }

    public void FailStart(PublicMatchState match)
    {
        ArgumentNullException.ThrowIfNull(match);
        lock (_sync)
        {
            match.IsCompleted = true;
            match.GameCompletion.TrySetResult(0);
            RemovePlayer(match.BlackTicket);
            RemovePlayer(match.WhiteTicket);
            match.BlackTicket.Notifications.Writer.TryComplete();
            match.WhiteTicket.Notifications.Writer.TryComplete();
        }
    }

    private void PairWaitingPlayers()
    {
        if (_waiting.Count < 2)
        {
            return;
        }

        MatchmakingTicket blackTicket = _waiting.First!.Value;
        MatchmakingTicket whiteTicket = _waiting.First.Next!.Value;
        _waiting.RemoveFirst();
        _waiting.RemoveFirst();
        blackTicket.QueueNode = null;
        whiteTicket.QueueNode = null;

        DateTimeOffset countdownEndsAt = _timeProvider.GetUtcNow()
            .Add(s_countdownDuration);
        var match = new PublicMatchState(
            Guid.NewGuid(), blackTicket, whiteTicket, countdownEndsAt);
        blackTicket.Match = match;
        whiteTicket.Match = match;
        PublishCountdown(match);
    }

    private static void PublishCountdown(PublicMatchState match)
    {
        long endTime = match.CountdownEndsAt.ToUnixTimeMilliseconds();
        MatchmakingEvent blackEvent = CreateCountdownEvent(
            match.WhiteTicket.Player.DisplayName, endTime);
        MatchmakingEvent whiteEvent = CreateCountdownEvent(
            match.BlackTicket.Player.DisplayName, endTime);
        match.BlackTicket.Notifications.Writer.TryWrite(blackEvent);
        match.WhiteTicket.Notifications.Writer.TryWrite(whiteEvent);
    }

    private void CancelCountdown(
        PublicMatchState match,
        MatchmakingTicket cancellingTicket)
    {
        match.IsCompleted = true;
        match.CountdownCancellation.Cancel();
        MatchmakingTicket remainingTicket = GetOtherTicket(match, cancellingTicket);
        cancellingTicket.Match = null;
        _activeTickets.Remove(cancellingTicket.Player.PlayerKey);
        cancellingTicket.Notifications.Writer.TryComplete();
        remainingTicket.Match = null;
        match.GameCompletion.TrySetResult(0);
        remainingTicket.Notifications.Writer.TryWrite(
            new MatchmakingEvent
            {
                MatchCountdownCancelled = new MatchCountdownCancelled()
            });
        remainingTicket.Notifications.Writer.TryWrite(
            new MatchmakingEvent { SearchQueued = new SearchQueued() });
        remainingTicket.QueueNode = _waiting.AddFirst(remainingTicket);
        PairWaitingPlayers();
    }

    private static MatchmakingTicket GetOtherTicket(
        PublicMatchState match,
        MatchmakingTicket ticket)
    {
        if (match.BlackTicket == ticket)
        {
            return match.WhiteTicket;
        }

        return match.BlackTicket;
    }

    private static MatchmakingEvent CreateCountdownEvent(
        string opponentName,
        long endTime)
    {
        return new MatchmakingEvent
        {
            MatchCountdownStarted = new MatchCountdownStarted
            {
                OpponentName = opponentName,
                CountdownEndsAtUnixMilliseconds = endTime
            }
        };
    }

    private static void PublishGameStarted(PublicMatchState match, long gameId)
    {
        MatchmakingEvent blackEvent = CreateGameStartedEvent(
            gameId,
            match.WhiteTicket.Player.DisplayName,
            DatabaseValues.StoneColors.Black);
        MatchmakingEvent whiteEvent = CreateGameStartedEvent(
            gameId,
            match.BlackTicket.Player.DisplayName,
            DatabaseValues.StoneColors.White);
        match.BlackTicket.Notifications.Writer.TryWrite(blackEvent);
        match.WhiteTicket.Notifications.Writer.TryWrite(whiteEvent);
    }

    private static MatchmakingEvent CreateGameStartedEvent(
        long gameId,
        string opponentName,
        string stoneColor)
    {
        return new MatchmakingEvent
        {
            PublicGameStarted = new PublicGameStarted
            {
                GameId = gameId,
                OpponentName = opponentName,
                StoneColor = stoneColor,
                CaptureGoal = PublicGameRules.CaptureGoal,
                GameTimeSeconds = PublicGameRules.GameTimeSeconds,
                TurnTimeSeconds = PublicGameRules.TurnTimeSeconds
            }
        };
    }

    private void RemovePlayer(MatchmakingTicket ticket)
    {
        _activeTickets.Remove(ticket.Player.PlayerKey);
        ticket.Match = null;
    }
}
