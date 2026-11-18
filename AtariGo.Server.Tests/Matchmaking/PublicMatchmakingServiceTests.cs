using AtariGo.Contracts;
using AtariGo.Server.Application.Matchmaking;
using AtariGo.Server.Tests.RoomInvitations;

namespace AtariGo.Server.Tests.Matchmaking;

[TestClass]
[TestCategory("Unit")]
public sealed class PublicMatchmakingServiceTests
{
    private static readonly TimeSpan completionTimeout = TimeSpan.FromSeconds(5);

    private readonly ManualTimeProvider _time = new ManualTimeProvider();
    private readonly ControlledPublicGameLifecycleService _lifecycle =
        new ControlledPublicGameLifecycleService();
    private readonly PublicMatchmakingService _service;

    public PublicMatchmakingServiceTests()
    {
        InMemoryPublicMatchmakingQueue queue = new InMemoryPublicMatchmakingQueue(_time);
        _service = new PublicMatchmakingService(queue, _lifecycle, _time);
    }

    [TestMethod]
    public async Task ReadEventsAsync_TimerWakesBeforeDeadline_WaitsAgainAndStartsGameAsync()
    {
        // Arrange
        AdvancingMatchmakingTimeProvider time = new AdvancingMatchmakingTimeProvider(true);
        InMemoryPublicMatchmakingQueue queue = new InMemoryPublicMatchmakingQueue(time);
        PublicMatchmakingService service = new PublicMatchmakingService(queue, _lifecycle, time);
        MatchmakingTicket? blackTicket = service.Join(new MatchmakingPlayer
        {
            PlayerKey = "Black",
            DisplayName = "Black"
        });
        service.Join(new MatchmakingPlayer { PlayerKey = "White", DisplayName = "White" });
        Assert.IsNotNull(blackTicket);
        using CancellationTokenSource timeout = new CancellationTokenSource(completionTimeout);
        await using IAsyncEnumerator<MatchmakingEvent> reader = service
            .ReadEventsAsync(blackTicket, timeout.Token).GetAsyncEnumerator();
        await ReadCountdownAsync(reader);
        _lifecycle.Completion.SetResult(42);
        // Act
        bool receivedGame = await reader.MoveNextAsync();
        // Assert
        Assert.IsTrue(receivedGame);
        Assert.AreEqual(42, reader.Current.PublicGameStarted.GameId);
        Assert.IsTrue(time.TimerCount >= 2);
        Assert.AreEqual(1, _lifecycle.CreationCount);
    }

    [TestMethod]
    public async Task ReadEventsAsync_TwoReadersAfterCountdown_CreatesOneGameForBothAsync()
    {
        // Arrange
        MatchmakingTicket blackTicket = Join("Black");
        MatchmakingTicket whiteTicket = Join("White");
        await using IAsyncEnumerator<MatchmakingEvent> black = CreateReader(blackTicket);
        await using IAsyncEnumerator<MatchmakingEvent> white = CreateReader(whiteTicket);
        await ReadCountdownAsync(black);
        await ReadCountdownAsync(white);
        _time.Advance(TimeSpan.FromSeconds(PublicGameRules.CountdownSeconds));
        // Act
        Task<bool> blackReading = black.MoveNextAsync().AsTask();
        await _lifecycle.Started.Task.WaitAsync(completionTimeout);
        Task<bool> whiteReading = white.MoveNextAsync().AsTask();
        bool waitedForCreation = !blackReading.IsCompleted && !whiteReading.IsCompleted;
        _lifecycle.Completion.SetResult(42);
        bool[] received = await Task.WhenAll(blackReading, whiteReading)
            .WaitAsync(completionTimeout);
        // Assert
        Assert.IsTrue(waitedForCreation);
        Assert.IsTrue(received.All(hasEvent => hasEvent));
        Assert.AreEqual(1, _lifecycle.CreationCount);
        Assert.AreEqual(42, black.Current.PublicGameStarted.GameId);
        Assert.AreEqual(42, white.Current.PublicGameStarted.GameId);
        Assert.AreEqual("Black", black.Current.PublicGameStarted.StoneColor);
        Assert.AreEqual("White", white.Current.PublicGameStarted.StoneColor);
        Assert.AreEqual(PublicGameRules.CaptureGoal, _lifecycle.Request!.Configuration.CaptureGoal);
        Assert.AreEqual(PublicGameRules.GameTimeSeconds,
            _lifecycle.Request.Configuration.GameTimeSeconds);
        Assert.AreEqual(PublicGameRules.TurnTimeSeconds,
            _lifecycle.Request.Configuration.TurnTimeSeconds);
        Assert.IsFalse(await black.MoveNextAsync());
        Assert.IsFalse(await white.MoveNextAsync());
    }

    [TestMethod]
    public async Task
        ReadEventsAsync_CancelledDuringCountdown_RequeuesOpponentWithoutCreatingGameAsync()
    {
        // Arrange
        MatchmakingTicket leavingTicket = Join("Leaving");
        MatchmakingTicket remainingTicket = Join("Remaining");
        using CancellationTokenSource cancellation = new CancellationTokenSource();
        await using IAsyncEnumerator<MatchmakingEvent> leaving = CreateReader(
            leavingTicket, cancellation.Token);
        await using IAsyncEnumerator<MatchmakingEvent> remaining = CreateReader(remainingTicket);
        await ReadCountdownAsync(leaving);
        await ReadCountdownAsync(remaining);
        // Act
        Task<bool> waiting = leaving.MoveNextAsync().AsTask();
        bool waitedForCountdown = !waiting.IsCompleted;
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => waiting.WaitAsync(
            completionTimeout));
        bool cancelled = await remaining.MoveNextAsync().AsTask().WaitAsync(completionTimeout);
        MatchmakingEvent.EventOneofCase cancellationEvent = remaining.Current.EventCase;
        bool requeued = await remaining.MoveNextAsync().AsTask().WaitAsync(completionTimeout);
        // Assert
        Assert.IsTrue(waitedForCountdown);
        Assert.IsTrue(cancelled && requeued);
        Assert.AreEqual(MatchmakingEvent.EventOneofCase.MatchCountdownCancelled, cancellationEvent);
        Assert.AreEqual(MatchmakingEvent.EventOneofCase.SearchQueued, remaining.Current.EventCase);
        Assert.AreEqual(0, _lifecycle.CreationCount);
    }

    [TestMethod]
    public async Task ReadEventsAsync_CreationFails_ClosesBothSearchesAndReleasesPlayersAsync()
    {
        // Arrange
        MatchmakingTicket blackTicket = Join("Black");
        MatchmakingTicket whiteTicket = Join("White");
        await using IAsyncEnumerator<MatchmakingEvent> black = CreateReader(blackTicket);
        await using IAsyncEnumerator<MatchmakingEvent> white = CreateReader(whiteTicket);
        await ReadCountdownAsync(black);
        await ReadCountdownAsync(white);
        _time.Advance(TimeSpan.FromSeconds(PublicGameRules.CountdownSeconds));
        // Act
        Task<bool> blackReading = black.MoveNextAsync().AsTask();
        await _lifecycle.Started.Task.WaitAsync(completionTimeout);
        Task<bool> whiteReading = white.MoveNextAsync().AsTask();
        _lifecycle.Completion.SetException(new InvalidOperationException("Creation failed."));
        InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => blackReading.WaitAsync(completionTimeout));
        bool receivedGame = await whiteReading.WaitAsync(completionTimeout);
        // Assert
        Assert.AreEqual("Creation failed.", failure.Message);
        Assert.IsFalse(receivedGame);
        Assert.AreEqual(1, _lifecycle.CreationCount);
        Assert.IsNotNull(Join("Black"));
        Assert.IsNotNull(Join("White"));
    }

    [TestMethod]
    public async Task ReadEventsAsync_CancelledAfterClaim_CompletesCreationForOpponentAsync()
    {
        // Arrange
        MatchmakingTicket blackTicket = Join("Black");
        MatchmakingTicket whiteTicket = Join("White");
        using CancellationTokenSource cancellation = new CancellationTokenSource();
        await using IAsyncEnumerator<MatchmakingEvent> black = CreateReader(
            blackTicket, cancellation.Token);
        await using IAsyncEnumerator<MatchmakingEvent> white = CreateReader(whiteTicket);
        await ReadCountdownAsync(black);
        await ReadCountdownAsync(white);
        _time.Advance(TimeSpan.FromSeconds(PublicGameRules.CountdownSeconds));
        // Act
        Task<bool> blackReading = black.MoveNextAsync().AsTask();
        await _lifecycle.Started.Task.WaitAsync(completionTimeout);
        Task<bool> whiteReading = white.MoveNextAsync().AsTask();
        cancellation.Cancel();
        bool continuedCreation = !blackReading.IsCompleted;
        _lifecycle.Completion.SetResult(42);
        await Assert.ThrowsAsync<OperationCanceledException>(() => blackReading.WaitAsync(
            completionTimeout));
        bool receivedGame = await whiteReading.WaitAsync(completionTimeout);
        // Assert
        Assert.IsTrue(continuedCreation);
        Assert.IsTrue(receivedGame);
        Assert.AreEqual(42, white.Current.PublicGameStarted.GameId);
        Assert.IsFalse(_lifecycle.CreationCancellation.CanBeCanceled);
        Assert.AreEqual(1, _lifecycle.CreationCount);
    }

    [TestMethod]
    public async Task ReadEventsAsync_CancelledBeforeClaim_DoesNotCreateGameAfterDeadlineAsync()
    {
        // Arrange
        MatchmakingTicket blackTicket = Join("Black");
        Join("White");
        using CancellationTokenSource cancellation = new CancellationTokenSource();
        await using IAsyncEnumerator<MatchmakingEvent> black = CreateReader(
            blackTicket, cancellation.Token);
        await ReadCountdownAsync(black);
        _time.Advance(TimeSpan.FromSeconds(PublicGameRules.CountdownSeconds));
        // Act
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => black.MoveNextAsync().AsTask());
        // Assert
        Assert.AreEqual(0, _lifecycle.CreationCount);
        Assert.IsNotNull(Join("Black"));
    }

    [TestMethod]
    public async Task ReadEventsAsync_ReaderDisposed_ReleasesQueuedPlayerAsync()
    {
        // Arrange
        MatchmakingTicket ticket = Join("Black");
        await using IAsyncEnumerator<MatchmakingEvent> reader = CreateReader(ticket);
        Assert.IsTrue(await reader.MoveNextAsync());
        // Act
        await reader.DisposeAsync();
        // Assert
        Assert.IsNotNull(Join("Black"));
        Assert.AreEqual(0, _lifecycle.CreationCount);
    }

    private MatchmakingTicket Join(string name)
    {
        MatchmakingTicket? ticket = _service.Join(new MatchmakingPlayer
        {
            PlayerKey = name,
            DisplayName = name,
            GuestName = name
        });
        return ticket ?? throw new InvalidOperationException("The test player was not queued.");
    }

    private IAsyncEnumerator<MatchmakingEvent> CreateReader(MatchmakingTicket ticket,
        CancellationToken cancellationToken = default)
    {
        return _service.ReadEventsAsync(ticket, cancellationToken).GetAsyncEnumerator();
    }

    private static async Task ReadCountdownAsync(IAsyncEnumerator<MatchmakingEvent> reader)
    {
        Assert.IsTrue(await reader.MoveNextAsync().AsTask().WaitAsync(completionTimeout));
        Assert.AreEqual(MatchmakingEvent.EventOneofCase.SearchQueued, reader.Current.EventCase);
        Assert.IsTrue(await reader.MoveNextAsync().AsTask().WaitAsync(completionTimeout));
        Assert.AreEqual(MatchmakingEvent.EventOneofCase.MatchCountdownStarted,
            reader.Current.EventCase);
    }
}
