using System.Text.Json;
using System.IO;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Grpc.Core;

using AtariGo.Diagnostics;
using AtariGo.GameLogic.Models;
using AtariGo.Server.Application.Games;
using AtariGo.Server.Data;
using AtariGo.Server.Data.Entities;
using AtariGo.Server.Application.Matchmaking;
using AtariGo.Server.Services;
using AtariGo.Server.Tests.Rooms;
using AtariGo.Server.Tests.Games;

namespace AtariGo.Server.Tests.Diagnostics;

[TestClass]
[TestCategory("Integration")]
public sealed class GameLoggingTests
{
    private readonly TestContext _testContext;

    public GameLoggingTests(TestContext testContext)
    {
        _testContext = testContext;
    }

    [TestMethod]
    public async Task Actions_ReplayAndConflict_KeepOneCommittedReceiptAsync()
    {
        // Arrange
        await using var logs = new ServerLogTestHost(fileLevel: "Information");
        await using GameplayTestDatabase database = await GameplayTestDatabase.CreateAsync(
            loggerFactory: logs.Application.Services.GetRequiredService<ILoggerFactory>());
        using GameSessionConnection black = await database.ConnectAsync("Black");
        using GameSessionConnection white = await database.ConnectAsync("White");
        GameActionCommand action = await database.CreateMoveAsync(new BoardPosition(0, 1));
        // Act
        await database.Coordinator.ProcessAsync(black, action, _testContext.CancellationToken);
        await database.Coordinator.ProcessAsync(black, action, _testContext.CancellationToken);
        await database.Coordinator.ProcessAsync(black, action with
        {
            Column = 2
        },
            _testContext.CancellationToken);
        List<JsonElement> entries = await logs.ReadEventsAsync(_testContext.CancellationToken);
        using AtariGoDbContext context = database.CreateContext();
        // Assert
        Assert.AreEqual(1, await context.Moves.CountAsync(_testContext.CancellationToken));
        Assert.AreEqual(1, await context.GameEvents.CountAsync(entry => entry.ActionId != null,
            _testContext.CancellationToken));
        Assert.HasCount(1, entries.Where(entry => ReadOperation(entry) == "ActionCommitted"));
        Assert.HasCount(1, entries.Where(entry => ReadOperation(entry) == "ActionReplayed"));
        JsonElement conflict = entries.Single(entry => ReadOperation(entry) == "ActionConflict");
        Assert.AreEqual(Guid.ParseExact(action.ActionId, "N"),
            conflict.GetProperty("ActionId").GetGuid());
        Assert.AreEqual("Rejected", conflict.GetProperty("Outcome").GetString());
        Assert.AreEqual(database.GameId, conflict.GetProperty("GameId").GetInt64());
        Assert.DoesNotContain(message => message.Contains("guest:", StringComparison.Ordinal),
            logs.Records.Messages);
    }

    [TestMethod]
    public async Task Action_FailedWrite_DoesNotLogACommittedActionAsync()
    {
        // Arrange
        await using var logs = new ServerLogTestHost(fileLevel: "Information");
        var interceptor = new GameWriteFailureInterceptor();
        await using GameplayTestDatabase database = await GameplayTestDatabase.CreateAsync(
            interceptor, logs.Application.Services.GetRequiredService<ILoggerFactory>());
        using GameSessionConnection black = await database.ConnectAsync("Black");
        using GameSessionConnection white = await database.ConnectAsync("White");
        GameActionCommand action = await database.CreateMoveAsync(new BoardPosition(0, 1));
        interceptor.FailMoveWrite = true;
        // Act
        await Assert.ThrowsAsync<DbUpdateException>(() => database.Coordinator.ProcessAsync(
            black, action, _testContext.CancellationToken));
        List<JsonElement> entries = await logs.ReadEventsAsync(_testContext.CancellationToken);
        using AtariGoDbContext context = database.CreateContext();
        // Assert
        Assert.AreEqual(0, await context.Moves.CountAsync(_testContext.CancellationToken));
        Assert.AreEqual(0, await context.GameEvents.CountAsync(entry => entry.ActionId != null,
            _testContext.CancellationToken));
        Assert.DoesNotContain(entry => ReadOperation(entry) == "ActionCommitted", entries);
    }

    [TestMethod]
    [DataRow(true, "Finished", "GameFinished")]
    [DataRow(false, "TurnTimedOut", "Timeout")]
    public async Task Transitions_RepeatedAdvance_LogOnlyCommittedChangesAsync(bool surrender,
        string operation, string auditType)
    {
        // Arrange
        await using var logs = new ServerLogTestHost(fileLevel: "Information");
        await using GameplayTestDatabase database = await GameplayTestDatabase.CreateAsync(
            loggerFactory: logs.Application.Services.GetRequiredService<ILoggerFactory>());
        using GameSessionConnection black = await database.ConnectAsync("Black");
        using GameSessionConnection white = await database.ConnectAsync("White");
        // Act
        if (surrender)
        {
            await database.Coordinator.ProcessAsync(black, new GameActionCommand
            {
                ActionId = Guid.NewGuid().ToString("N"),
                IsSurrender = true
            }, _testContext.CancellationToken);
        }
        else
        {
            database.Time.Advance(TimeSpan.FromSeconds(31));
        }
        await database.Coordinator.AdvanceAsync(database.GameId, _testContext.CancellationToken);
        await database.Coordinator.AdvanceAsync(database.GameId, _testContext.CancellationToken);
        List<JsonElement> entries = await logs.ReadEventsAsync(_testContext.CancellationToken);
        using AtariGoDbContext context = database.CreateContext();
        // Assert
        Assert.HasCount(1, entries.Where(entry => ReadOperation(entry) == operation));
        Assert.HasCount(1, entries.Where(entry => ReadOperation(entry) == "Started"));
        Assert.AreEqual(1, await context.GameEvents.CountAsync(
            entry => entry.EventType == auditType,
            _testContext.CancellationToken));
    }

    [TestMethod]
    [DataRow("user:1", "private-chat", "Accepted")]
    [DataRow("user:1", " ", "Empty")]
    [DataRow("guest:private-key", "private-chat", "GuestNotAllowed")]
    public async Task Chat_Submission_RecordsOutcomeWithoutTextOrPlayerKeyAsync(string playerKey,
        string text, string expectedStatus)
    {
        // Arrange
        await using var logs = new ServerLogTestHost(fileLevel: "Information");
        await using GameplayTestDatabase database = await GameplayTestDatabase.CreateAsync(
            loggerFactory: logs.Application.Services.GetRequiredService<ILoggerFactory>());
        using GameSessionConnection connection = database.Registry.Connect(new GameSessionDefinition
        {
            GameId = database.GameId,
            CaptureGoal = 1,
            TurnTimeSeconds = 30,
            Player = new GameSessionParticipant(playerKey, "private-name", "Black"),
            Opponent = new GameSessionParticipant("user:2", "private-opponent", "White")
        });
        // Act
        await database.Coordinator.PublishChatMessageAsync(connection, text,
            _testContext.CancellationToken);
        List<JsonElement> entries = await logs.ReadEventsAsync(_testContext.CancellationToken);
        JsonElement submission = entries.Single(entry => ReadOperation(entry) == "ChatSubmission");
        // Assert
        Assert.AreEqual(expectedStatus, submission.GetProperty("Status").GetProperty("Value")
            .GetString());
        Assert.DoesNotContain(message => message.Contains("private", StringComparison.Ordinal),
            logs.Records.Messages);
    }

    [TestMethod]
    public async Task Stream_InterruptedTransport_PreservesSafeCauseAndConnectionIdentityAsync()
    {
        // Arrange
        await using var logs = new ServerLogTestHost(fileLevel: "Information");
        await using GameplayTestDatabase database = await GameplayTestDatabase.CreateAsync(
            loggerFactory: logs.Application.Services.GetRequiredService<ILoggerFactory>());
        using AtariGoDbContext context = database.CreateContext();
        var identities = new GuestIdentityRegistry(TimeProvider.System);
        IssuedGuestIdentity identity = identities.Create("private-player");
        GamePlayer player = await context.GamePlayers.SingleAsync(
            entry => entry.StoneColor == "Black",
            _testContext.CancellationToken);
        player.GuestIdentityId = identity.Identity.GuestId;
        await context.SaveChangesAsync(_testContext.CancellationToken);
        var service = new GameSessionGrpcService(new GameSessionCatalog(context),
            database.Coordinator,
            identities, new TestRoomSessionService(),
            logs.Application.Services.GetRequiredService<ILogger<GameSessionGrpcService>>());
        var transport = new FailingGameSessionTransport(database.GameId,
            new IOException("private-failure"));
        // Act
        RpcException error = await Assert.ThrowsAsync<RpcException>(() => service.Connect(
            transport, transport, new GameSessionTestCallContext(identity.Credential)));
        List<JsonElement> entries = await logs.ReadEventsAsync(_testContext.CancellationToken);
        // Assert
        Assert.AreEqual(StatusCode.Unavailable, error.StatusCode);
        JsonElement failure = entries.Single(entry => ReadOperation(entry) == "StreamFailure");
        JsonElement connected = entries.Single(entry => ReadOperation(entry) == "Connected");
        Assert.AreEqual("Unconfirmed", failure.GetProperty("Outcome").GetString());
        Assert.AreEqual(connected.GetProperty("OperationId").GetGuid(),
            failure.GetProperty("OperationId").GetGuid());
        Assert.AreEqual(nameof(IOException),
            failure.GetProperty("Error").GetProperty("TypeName").GetString());
        Assert.DoesNotContain(message => message.Contains("private", StringComparison.Ordinal)
            || message.Contains(identity.Credential, StringComparison.Ordinal),
            logs.Records.Messages);
    }

    [TestMethod]
    public async Task Queue_SlowConsumer_LogsSaturationOnceWithoutChangingCapacityAsync()
    {
        // Arrange
        await using var logs = new ServerLogTestHost(fileLevel: "Information");
        var registry = new GameSessionRegistry(TimeProvider.System,
            logs.Application.Services.GetRequiredService<ILogger<GameSessionRegistry>>());
        using GameSessionConnection connection = registry.Connect(new GameSessionDefinition
        {
            GameId = 1,
            CaptureGoal = 1,
            TurnTimeSeconds = 30,
            Player = new GameSessionParticipant("user:1", "private", "Black"),
            Opponent = new GameSessionParticipant("user:2", "private", "White")
        });
        // Act
        for (int index = 0; index < GameSessionRules.MaximumPendingEvents + 2; index++)
        {
            registry.PublishProgress(1, new GameRuntimeState(), index);
        }
        List<JsonElement> entries = await logs.ReadEventsAsync(_testContext.CancellationToken);
        // Assert
        Assert.HasCount(1, entries.Where(entry => ReadOperation(entry) == "Backpressure"));
        Assert.AreEqual(GameSessionRules.MaximumPendingEvents, connection.Events.Count);
    }

    private static string ReadOperation(JsonElement entry)
    {
        return entry.GetProperty("Operation").GetString()!;
    }
}
