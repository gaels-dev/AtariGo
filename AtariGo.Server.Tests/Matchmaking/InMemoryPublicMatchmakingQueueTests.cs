
using AtariGo.Contracts;
using AtariGo.Server.Application.Matchmaking;

namespace AtariGo.Server.Tests.Matchmaking;

[TestClass]
[TestCategory("Unit")]
public sealed class InMemoryPublicMatchmakingQueueTests
{
    [TestMethod]
    public void Join_TwoPlayersWaiting_PairsFifoPlayers()
    {
        // Arrange
        InMemoryPublicMatchmakingQueue queue = new InMemoryPublicMatchmakingQueue(
            TimeProvider.System);
        MatchmakingTicket firstTicket = JoinPlayer(queue, "first", "First");

        // Act
        MatchmakingTicket secondTicket = JoinPlayer(queue, "second", "Second");

        // Assert
        MatchmakingEvent firstQueuedEvent = ReadEvent(firstTicket);
        Assert.AreEqual(
            MatchmakingEvent.EventOneofCase.SearchQueued,
            firstQueuedEvent.EventCase);
        MatchmakingEvent firstCountdownEvent = ReadEvent(firstTicket);
        Assert.AreEqual(
            MatchmakingEvent.EventOneofCase.MatchCountdownStarted,
            firstCountdownEvent.EventCase);
        Assert.AreEqual("Second", firstCountdownEvent.MatchCountdownStarted.OpponentName);
        MatchmakingEvent secondQueuedEvent = ReadEvent(secondTicket);
        Assert.AreEqual(
            MatchmakingEvent.EventOneofCase.SearchQueued,
            secondQueuedEvent.EventCase);
        MatchmakingEvent secondCountdownEvent = ReadEvent(secondTicket);
        Assert.AreEqual("First", secondCountdownEvent.MatchCountdownStarted.OpponentName);
        Assert.AreEqual(
            firstCountdownEvent.MatchCountdownStarted.CountdownEndsAtUnixMilliseconds,
            secondCountdownEvent.MatchCountdownStarted.CountdownEndsAtUnixMilliseconds);
    }

    [TestMethod]
    public void Disconnect_DuringCountdown_RequeuesRemainingPlayer()
    {
        // Arrange
        InMemoryPublicMatchmakingQueue queue = new InMemoryPublicMatchmakingQueue(
            TimeProvider.System);
        MatchmakingTicket leavingTicket = JoinPlayer(queue, "leaving", "Leaving");
        MatchmakingTicket remainingTicket = JoinPlayer(
            queue, "remaining", "Remaining");

        // Act
        queue.Disconnect(leavingTicket);

        // Assert
        MatchmakingEvent queuedEvent = ReadEvent(remainingTicket);
        Assert.AreEqual(MatchmakingEvent.EventOneofCase.SearchQueued, queuedEvent.EventCase);
        MatchmakingEvent countdownEvent = ReadEvent(remainingTicket);
        Assert.AreEqual(
            MatchmakingEvent.EventOneofCase.MatchCountdownStarted,
            countdownEvent.EventCase);
        MatchmakingEvent cancelledEvent = ReadEvent(remainingTicket);
        Assert.AreEqual(
            MatchmakingEvent.EventOneofCase.MatchCountdownCancelled,
            cancelledEvent.EventCase);
        MatchmakingEvent requeuedEvent = ReadEvent(remainingTicket);
        Assert.AreEqual(
            MatchmakingEvent.EventOneofCase.SearchQueued,
            requeuedEvent.EventCase);

        MatchmakingTicket nextTicket = JoinPlayer(queue, "next", "Next");
        MatchmakingEvent nextCountdownEvent = ReadEvent(remainingTicket);
        Assert.AreEqual(
            MatchmakingEvent.EventOneofCase.MatchCountdownStarted,
            nextCountdownEvent.EventCase);
        Assert.AreEqual("Next", nextCountdownEvent.MatchCountdownStarted.OpponentName);
        MatchmakingEvent joinedEvent = ReadEvent(nextTicket);
        Assert.AreEqual(MatchmakingEvent.EventOneofCase.SearchQueued, joinedEvent.EventCase);
        MatchmakingEvent pairedEvent = ReadEvent(nextTicket);
        Assert.AreEqual(
            MatchmakingEvent.EventOneofCase.MatchCountdownStarted,
            pairedEvent.EventCase);
    }

    [TestMethod]
    public void Join_SamePlayerAlreadyQueued_RejectsDuplicateTicket()
    {
        // Arrange
        InMemoryPublicMatchmakingQueue queue = new InMemoryPublicMatchmakingQueue(
            TimeProvider.System);
        JoinPlayer(queue, "same", "Same");

        // Act
        MatchmakingTicket? duplicateTicket = queue.Join(
            CreatePlayer("same", "Same"));

        // Assert
        Assert.IsNull(duplicateTicket);
    }

    private static MatchmakingPlayer CreatePlayer(string key, string displayName)
    {
        return new MatchmakingPlayer
        {
            PlayerKey = key,
            DisplayName = displayName,
            GuestName = displayName
        };
    }

    private static MatchmakingTicket JoinPlayer(
        InMemoryPublicMatchmakingQueue queue,
        string key,
        string displayName)
    {
        MatchmakingTicket? ticket = queue.Join(CreatePlayer(key, displayName));
        if (ticket is null)
        {
            throw new InvalidOperationException("The test player was not queued.");
        }

        return ticket;
    }

    private static MatchmakingEvent ReadEvent(MatchmakingTicket ticket)
    {
        if (!ticket.Events.TryRead(out MatchmakingEvent? matchmakingEvent)
            || matchmakingEvent is null)
        {
            throw new InvalidOperationException("The expected event was not queued.");
        }

        return matchmakingEvent;
    }
}
