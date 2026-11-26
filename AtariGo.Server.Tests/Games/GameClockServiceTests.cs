using System.Data.Common;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using AtariGo.Server.Application.Games;
using AtariGo.Server.Data;
using AtariGo.Server.Data.Entities;
using AtariGo.Server.Tests.RoomInvitations;

namespace AtariGo.Server.Tests.Games;

[TestClass]
[TestCategory("Integration")]
public sealed class GameClockServiceTests
{
    private static readonly TimeSpan completionTimeout = TimeSpan.FromSeconds(5);

    [TestMethod]
    public async Task ClockTick_ExpiredWaitingGame_CancelsAfterConnectionGraceAsync()
    {
        // Arrange
        await using GameplayTestDatabase database = await GameplayTestDatabase.CreateAsync();
        await database.ConnectAsync("Black");
        database.Time.Advance(TimeSpan.FromSeconds(GameRuntimeState.ConnectionGraceSeconds));
        ClockTimeProvider clockTime = new ClockTimeProvider(database.Time);
        CycleRegistry registry = new CycleRegistry();
        using GameClockService clock = CreateClock(database, clockTime, registry);
        await clock.StartAsync(CancellationToken.None);
        await clockTime.Ready.WaitAsync(completionTimeout);

        try
        {
            // Act
            clockTime.Tick();
            await registry.CycleCompleted.WaitAsync(completionTimeout);
            Game game = await database.ReadAsync();

            // Assert
            Assert.AreEqual("Cancelled", game.State);
            Assert.AreEqual("ConnectionTimeout", game.EndReason);
            Assert.IsTrue(game.GamePlayers.All(player => player.Result != "Won"));
        }
        finally
        {
            await clock.StopAsync(CancellationToken.None);
        }
    }

    [TestMethod]
    public async Task ClockTick_FutureDeadline_DoesNotAdvanceGameAsync()
    {
        // Arrange
        await using GameplayTestDatabase database = await GameplayTestDatabase.CreateAsync();
        ClockTimeProvider clockTime = new ClockTimeProvider(database.Time);
        CycleRegistry registry = new CycleRegistry();
        using GameClockService clock = CreateClock(database, clockTime, registry);
        Game before = await database.ReadAsync();
        await clock.StartAsync(CancellationToken.None);
        await clockTime.Ready.WaitAsync(completionTimeout);

        try
        {
            // Act
            clockTime.Tick();
            await registry.CycleCompleted.WaitAsync(completionTimeout);
            Game after = await database.ReadAsync();

            // Assert
            Assert.AreEqual(GameRuntimeState.Waiting, after.State);
            Assert.AreEqual(before.StateVersion, after.StateVersion);
            Assert.AreEqual(before.TurnDeadlineUtc, after.TurnDeadlineUtc);
        }
        finally
        {
            await clock.StopAsync(CancellationToken.None);
        }
    }

    [TestMethod]
    public async Task ClockTick_OrphanedGame_CancelsWithoutWinnerOrRepeatedTransitionsAsync()
    {
        // Arrange
        await using GameplayTestDatabase database = await GameplayTestDatabase.CreateAsync();
        GameSessionConnection black = await database.ConnectAsync("Black");
        GameSessionConnection white = await database.ConnectAsync("White");
        long initialVersion = (await database.ReadAsync()).StateVersion;
        database.Registry.Disconnect(black);
        database.Registry.Disconnect(white);
        database.Time.Advance(TimeSpan.FromHours(2));
        database.Registry.PruneIdleSessions();
        ClockTimeProvider clockTime = new ClockTimeProvider(database.Time);
        CycleRegistry registry = new CycleRegistry();
        using GameClockService clock = CreateClock(database, clockTime, registry);
        await clock.StartAsync(CancellationToken.None);
        await clockTime.Ready.WaitAsync(completionTimeout);

        try
        {
            // Act
            clockTime.Tick();
            await registry.CycleCompleted.WaitAsync(completionTimeout);
            Game cancelled = await database.ReadAsync();
            await database.Coordinator.AdvanceAsync(database.GameId, CancellationToken.None);
            Game unchanged = await database.ReadAsync();

            // Assert
            Assert.AreEqual(GameRuntimeState.Cancelled, cancelled.State);
            Assert.AreEqual("BothDisconnected", cancelled.EndReason);
            Assert.AreEqual(initialVersion + 1, unchanged.StateVersion);
            Assert.IsTrue(unchanged.GamePlayers.All(player => player.Result == "Pending"));
            using AtariGoDbContext context = database.CreateContext();
            Assert.AreEqual(1, await context.GameEvents.CountAsync(entry =>
                entry.GameId == database.GameId && entry.EventType == "GameFinished"));
        }
        finally
        {
            await clock.StopAsync(CancellationToken.None);
        }
    }

    [TestMethod]
    public async Task AdvanceAsync_MissingSessionBeforeDeadline_CancelsWithoutWinnerAsync()
    {
        // Arrange
        await using GameplayTestDatabase database = await GameplayTestDatabase.CreateAsync();
        await database.ConnectAsync("Black");
        await database.ConnectAsync("White");
        Game before = await database.ReadAsync();
        using IServiceScope scope = database.Services.CreateScope();
        GameCommandService commands = new GameCommandService(
            scope.ServiceProvider.GetRequiredService<GamePersistenceService>(),
            scope.ServiceProvider.GetRequiredService<GameActionProcessor>(),
            database.Time,
            new CycleRegistry());
        GameOperationContext operation = await commands.LoadAsync(database.GameId,
            CancellationToken.None);

        // Act
        GameOperationResult result = await commands.AdvanceAsync(operation, () => false);
        Game after = await database.ReadAsync();

        // Assert
        Assert.AreEqual(GameRuntimeState.Cancelled, result.Progress?.Status);
        Assert.AreEqual(before.StateVersion + 1, after.StateVersion);
        Assert.AreEqual(before.TurnDeadlineUtc, after.TurnDeadlineUtc);
        Assert.IsTrue(after.GamePlayers.All(player => player.Result == "Pending"));
    }

    [TestMethod]
    public async Task ClockTick_DatabaseReadFails_ProcessesNextTickAsync()
    {
        // Arrange
        FailingGameReadInterceptor interceptor = new FailingGameReadInterceptor();
        await using GameplayTestDatabase database = await GameplayTestDatabase.CreateAsync(
            interceptor);
        await database.ConnectAsync("Black");
        database.Time.Advance(TimeSpan.FromSeconds(GameRuntimeState.ConnectionGraceSeconds));
        ClockTimeProvider clockTime = new ClockTimeProvider(database.Time);
        CycleRegistry registry = new CycleRegistry();
        ErrorRecordingLogger logger = new ErrorRecordingLogger();
        IServiceScopeFactory scopes = database.Services.GetRequiredService<IServiceScopeFactory>();
        using GameClockService clock = new GameClockService(scopes, database.Coordinator,
            registry, clockTime, logger);
        await clock.StartAsync(CancellationToken.None);
        await clockTime.Ready.WaitAsync(completionTimeout);

        try
        {
            // Act
            interceptor.FailNextRead = true;
            clockTime.Tick();
            await logger.ErrorRecorded.WaitAsync(completionTimeout);
            clockTime.Tick();
            await registry.CycleCompleted.WaitAsync(completionTimeout);
            Game game = await database.ReadAsync();

            // Assert
            Assert.IsTrue(interceptor.FailedRead);
            Assert.AreEqual("Cancelled", game.State);
            Assert.AreEqual("ConnectionTimeout", game.EndReason);
        }
        finally
        {
            await clock.StopAsync(CancellationToken.None);
        }
    }

    private static GameClockService CreateClock(GameplayTestDatabase database,
        ClockTimeProvider clockTime, CycleRegistry registry)
    {
        IServiceScopeFactory scopes = database.Services.GetRequiredService<IServiceScopeFactory>();
        return new GameClockService(scopes, database.Coordinator, registry, clockTime,
            NullLogger<GameClockService>.Instance);
    }

    private sealed class FailingGameReadInterceptor : DbCommandInterceptor
    {
        public bool FailNextRead { get; set; }

        public bool FailedRead { get; private set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (FailNextRead && command.CommandText.Contains("FROM \"Games\"",
                StringComparison.Ordinal))
            {
                FailNextRead = false;
                FailedRead = true;
                throw new SqliteException("Simulated game lookup failure.", 1);
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class ErrorRecordingLogger : ILogger<GameClockService>
    {
        private readonly TaskCompletionSource _errorRecorded =
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task ErrorRecorded => _errorRecorded.Task;

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            return NullLogger<GameClockService>.Instance.BeginScope(state);
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Error)
            {
                _errorRecorded.TrySetResult();
            }
        }
    }

    private sealed class ClockTimeProvider : TimeProvider
    {
        private readonly ManualTimeProvider _currentTime;
        private readonly TaskCompletionSource _ready =
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        private ManualTimer? _timer;

        public ClockTimeProvider(ManualTimeProvider currentTime)
        {
            _currentTime = currentTime;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _currentTime.GetUtcNow();
        }

        public Task Ready => _ready.Task;

        public override ITimer CreateTimer(TimerCallback callback, object? state,
            TimeSpan dueTime, TimeSpan period)
        {
            _timer = new ManualTimer(callback, state);
            _ready.TrySetResult();
            return _timer;
        }

        public void Tick()
        {
            ManualTimer timer = _timer
                ?? throw new InvalidOperationException("The game clock has not started.");
            timer.Tick();
        }
    }

    private sealed class ManualTimer : ITimer
    {
        private readonly TimerCallback _callback;
        private readonly object? _state;
        private int _disposed;

        public ManualTimer(TimerCallback callback, object? state)
        {
            _callback = callback;
            _state = state;
        }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            return Volatile.Read(ref _disposed) == 0;
        }

        public void Tick()
        {
            if (Volatile.Read(ref _disposed) == 0)
            {
                _callback(_state);
            }
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _disposed, 1);
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class CycleRegistry : IGameSessionRegistry
    {
        private readonly TaskCompletionSource _cycleCompleted =
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task CycleCompleted => _cycleCompleted.Task;

        public GameSessionConnection Connect(GameSessionDefinition definition)
        {
            throw new NotSupportedException();
        }

        public ChatMessageSubmissionResult PublishChatMessage(GameSessionConnection connection,
            string text)
        {
            throw new NotSupportedException();
        }

        public ChatMessageSubmissionResult RejectChatMessage(GameSessionConnection connection,
            ChatMessageRejectionReason reason)
        {
            throw new NotSupportedException();
        }

        public bool IsCurrent(GameSessionConnection connection)
        {
            throw new NotSupportedException();
        }

        public bool AreBothConnected(long gameId)
        {
            throw new NotSupportedException();
        }

        public GameConnectionPresence GetPresence(long gameId)
        {
            return new GameConnectionPresence(false, false);
        }

        public GameRuntimeState? GetProgress(long gameId, long version)
        {
            return null;
        }

        public void PublishProgress(long gameId, GameRuntimeState progress, long version)
        {
            throw new NotSupportedException();
        }

        public void PublishActionResult(GameSessionConnection connection,
            GameActionResult result)
        {
            throw new NotSupportedException();
        }

        public void PruneIdleSessions()
        {
            _cycleCompleted.TrySetResult();
        }

        public void Disconnect(GameSessionConnection connection)
        {
            throw new NotSupportedException();
        }

        public void DisconnectUser(int userId)
        {
            throw new NotSupportedException();
        }
    }
}
