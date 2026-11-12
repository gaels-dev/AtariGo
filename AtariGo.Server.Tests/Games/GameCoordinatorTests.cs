using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using AtariGo.GameLogic.Models;
using AtariGo.Server.Application.Games;
using AtariGo.Server.Data;
using AtariGo.Server.Data.Entities;

namespace AtariGo.Server.Tests.Games;

[TestClass]
[TestCategory("Integration")]
public sealed class GameCoordinatorTests
{
    [TestMethod]
    public async Task ProcessAsync_RepeatedAction_StoresOneMoveAndRestoresReceipt()
    {
        // Arrange
        await using GameplayTestDatabase database = await GameplayTestDatabase.CreateAsync();
        GameSessionConnection black = await database.ConnectAsync("Black");
        await database.ConnectAsync("White");
        GameActionCommand move = await database.CreateMoveAsync(new BoardPosition(0, 1));
        // Act
        await database.Coordinator.ProcessAsync(black, move, CancellationToken.None);
        await database.Coordinator.ProcessAsync(black, move, CancellationToken.None);
        GameActionResult result = await ReadResultAsync(black);
        GameActionResult repeated = await ReadResultAsync(black);
        using IServiceScope scope = database.Services.CreateScope();
        AtariGoDbContext context = scope.ServiceProvider.GetRequiredService<AtariGoDbContext>();
        // Assert
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(result, repeated);
        Assert.AreEqual(1, await context.Moves.CountAsync());
        Assert.AreEqual(1, await context.GameEvents.CountAsync(entry => entry.ActionId != null));
    }

    [TestMethod]
    public async Task AdvanceAsync_ExpiredTurn_SwitchesAndResetsWithoutDefeat()
    {
        // Arrange
        await using GameplayTestDatabase database = await GameplayTestDatabase.CreateAsync();
        await database.ConnectAsync("Black");
        await database.ConnectAsync("White");
        database.Time.Advance(TimeSpan.FromSeconds(30));
        // Act
        await database.Coordinator.AdvanceAsync(database.GameId, CancellationToken.None);
        GameRuntimeState state = GamePersistenceService.Read(await database.ReadAsync());
        // Assert
        Assert.AreEqual(StoneColor.White, state.Board.CurrentTurn);
        Assert.AreEqual(GameRuntimeState.Playing, state.Status);
        Assert.AreEqual(database.Time.GetUtcNow().UtcDateTime.AddSeconds(30), state.DeadlineUtc);
        Assert.AreEqual(StoneColor.Empty, state.Board.Winner);
        Assert.AreEqual(2, state.Board.TurnNumber);
    }

    [TestMethod]
    public async Task ProcessAsync_MoveArrivesAtDeadline_RejectsOldTurn()
    {
        // Arrange
        await using GameplayTestDatabase database = await GameplayTestDatabase.CreateAsync();
        GameSessionConnection black = await database.ConnectAsync("Black");
        await database.ConnectAsync("White");
        GameActionCommand move = await database.CreateMoveAsync(new BoardPosition(0, 0));
        database.Time.Advance(TimeSpan.FromSeconds(30));
        // Act
        await database.Coordinator.ProcessAsync(black, move, CancellationToken.None);
        GameActionResult result = await ReadResultAsync(black);
        // Assert
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual("StaleVersion", result.Rejection);
        GameRuntimeState state = GamePersistenceService.Read(await database.ReadAsync());
        Assert.IsTrue(state.Board.Board.All(stone => stone == StoneColor.Empty));
        Assert.AreEqual(StoneColor.White, state.Board.CurrentTurn);
    }

    [TestMethod]
    public async Task ProcessAsync_CaptureGoal_PersistsWinnerAndRejectsFurtherMoves()
    {
        // Arrange
        await using GameplayTestDatabase database = await GameplayTestDatabase.CreateAsync();
        GameSessionConnection black = await database.ConnectAsync("Black");
        GameSessionConnection white = await database.ConnectAsync("White");
        await PlayAsync(database, black, new BoardPosition(0, 1));
        await PlayAsync(database, white, new BoardPosition(0, 0));
        // Act
        await PlayAsync(database, black, new BoardPosition(1, 0));
        Game progress = await database.ReadAsync();
        GameRuntimeState state = GamePersistenceService.Read(progress);
        await PlayAsync(database, white, new BoardPosition(8, 8));
        GameActionResult rejected = await ReadResultAsync(white);
        // Assert
        Assert.AreEqual("Completed", progress.State);
        Assert.AreEqual("CaptureGoal", progress.EndReason);
        Assert.IsNotNull(progress.FinishedAt);
        Assert.AreEqual(StoneColor.Black, state.Board.Winner);
        Assert.AreEqual("Won", progress.GamePlayers.Single(
            player => player.StoneColor == "Black").Result);
        Assert.IsTrue(rejected.IsAccepted); // Earlier white move receipt.
        Assert.AreEqual("Finished", (await ReadResultAsync(white)).Rejection);
    }

    [TestMethod]
    public async Task ConnectAsync_BothPlayersDisconnected_RestoresBoardAndDeadline()
    {
        // Arrange
        await using GameplayTestDatabase database = await GameplayTestDatabase.CreateAsync();
        GameSessionConnection black = await database.ConnectAsync("Black");
        GameSessionConnection white = await database.ConnectAsync("White");
        await PlayAsync(database, black, new BoardPosition(0, 1));
        Game before = await database.ReadAsync();
        database.Registry.Disconnect(black);
        database.Registry.Disconnect(white);
        database.Time.Advance(TimeSpan.FromSeconds(5));
        // Act
        GameSessionConnection restored = await database.ConnectAsync("White");
        Game after = await database.ReadAsync();
        using CancellationTokenSource timeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(5));
        GameSessionDelivery delivery = await restored.Events.ReadAsync(timeout.Token);
        // Assert
        Assert.AreEqual(GamePersistenceService.Read(before).DeadlineUtc,
            GamePersistenceService.Read(after).DeadlineUtc);
        Assert.AreEqual("Black", delivery.Snapshot!.Cells[1].Stone);
        Assert.AreEqual(StoneColor.White, delivery.Snapshot.Progress!.Board.CurrentTurn);
    }

    [TestMethod]
    public async Task ProcessAsync_Surrender_CompletesForBothPlayers()
    {
        // Arrange
        await using GameplayTestDatabase database = await GameplayTestDatabase.CreateAsync();
        GameSessionConnection black = await database.ConnectAsync("Black");
        await database.ConnectAsync("White");
        GameActionCommand command = new GameActionCommand
        {
            ActionId = Guid.NewGuid().ToString("N"),
            IsSurrender = true
        };
        // Act
        await database.Coordinator.ProcessAsync(black, command, CancellationToken.None);
        Game progress = await database.ReadAsync();
        // Assert
        Assert.AreEqual("Surrender", progress.EndReason);
        Assert.AreEqual("Surrendered", progress.GamePlayers.Single(
            player => player.StoneColor == "Black").Result);
        Assert.AreEqual("Won", progress.GamePlayers.Single(
            player => player.StoneColor == "White").Result);
    }

    [TestMethod]
    public async Task AdvanceAsync_MissingOpponent_CancelsWithoutWinner()
    {
        // Arrange
        await using GameplayTestDatabase database = await GameplayTestDatabase.CreateAsync();
        await database.ConnectAsync("Black");
        database.Time.Advance(TimeSpan.FromSeconds(GameRuntimeState.ConnectionGraceSeconds));
        // Act
        await database.Coordinator.AdvanceAsync(database.GameId, CancellationToken.None);
        Game progress = await database.ReadAsync();
        // Assert
        Assert.AreEqual("Cancelled", progress.State);
        Assert.AreEqual("ConnectionTimeout", progress.EndReason);
        Assert.IsTrue(progress.GamePlayers.All(player => player.Result != "Won"));
    }

    [TestMethod]
    public async Task ProcessAsync_ConcurrentSameTurn_OnlyOneMoveIsAccepted()
    {
        // Arrange
        await using GameplayTestDatabase database = await GameplayTestDatabase.CreateAsync();
        GameSessionConnection black = await database.ConnectAsync("Black");
        await database.ConnectAsync("White");
        GameActionCommand first = await database.CreateMoveAsync(new BoardPosition(0, 0));
        GameActionCommand second = await database.CreateMoveAsync(new BoardPosition(0, 1));
        // Act
        await Task.WhenAll(database.Coordinator.ProcessAsync(black, first, CancellationToken.None),
            database.Coordinator.ProcessAsync(black, second, CancellationToken.None));
        GameActionResult firstResult = await ReadResultAsync(black);
        GameActionResult secondResult = await ReadResultAsync(black);
        // Assert
        Assert.IsTrue(firstResult.IsAccepted);
        Assert.IsFalse(secondResult.IsAccepted);
    }

    [TestMethod]
    [DataRow(30, StoneColor.White, 2)]
    [DataRow(31, StoneColor.White, 2)]
    [DataRow(60, StoneColor.Black, 3)]
    [DataRow(61, StoneColor.Black, 3)]
    public async Task ProcessAsync_TurnExpiresDuringLookup_RejectsAndPersistsTimeout(
        int elapsedSeconds, StoneColor expectedTurn, int expectedTurnNumber)
    {
        // Arrange
        GameActionLookupInterceptor interceptor = new GameActionLookupInterceptor();
        await using GameplayTestDatabase database = await GameplayTestDatabase.CreateAsync(
            interceptor);
        GameSessionConnection black = await database.ConnectAsync("Black");
        await database.ConnectAsync("White");
        GameActionCommand move = await database.CreateMoveAsync(new BoardPosition(0, 1));
        DateTime startedAt = database.Time.GetUtcNow().UtcDateTime;
        interceptor.LookupCallback = () => database.Time.Advance(
            TimeSpan.FromSeconds(elapsedSeconds));
        // Act
        await database.Coordinator.ProcessAsync(black, move, CancellationToken.None);
        GameActionResult result = await ReadResultAsync(black);
        Game progress = await database.ReadAsync();
        GameRuntimeState state = GamePersistenceService.Read(progress);
        using AtariGoDbContext context = database.CreateContext();
        GameEvent receipt = await context.GameEvents.SingleAsync(entry => entry.ActionId != null);
        // Assert
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual("StaleVersion", result.Rejection);
        Assert.AreEqual(expectedTurn, state.Board.CurrentTurn);
        Assert.AreEqual(expectedTurnNumber, state.Board.TurnNumber);
        Assert.AreEqual(startedAt.AddSeconds(expectedTurnNumber * 30), state.DeadlineUtc);
        Assert.AreEqual(move.ExpectedVersion + 1, progress.StateVersion);
        Assert.AreEqual(progress.StateVersion, result.Version);
        Assert.IsTrue(state.Board.Board.All(stone => stone == StoneColor.Empty));
        Assert.AreEqual(0, await context.Moves.CountAsync());
        Assert.AreEqual(1, await context.GameEvents.CountAsync(
            entry => entry.EventType == "Timeout"));
        Assert.AreEqual("ActionRejected", receipt.EventType);
        Assert.AreEqual(database.Time.GetUtcNow().UtcDateTime, receipt.CreatedAt);
    }

    [TestMethod]
    public async Task ProcessAsync_LookupFinishesBeforeDeadline_StartsFreshOpponentClock()
    {
        // Arrange
        GameActionLookupInterceptor interceptor = new GameActionLookupInterceptor();
        await using GameplayTestDatabase database = await GameplayTestDatabase.CreateAsync(
            interceptor);
        GameSessionConnection black = await database.ConnectAsync("Black");
        await database.ConnectAsync("White");
        GameActionCommand move = await database.CreateMoveAsync(new BoardPosition(0, 1));
        interceptor.LookupCallback = () => database.Time.Advance(TimeSpan.FromSeconds(29));
        // Act
        await database.Coordinator.ProcessAsync(black, move, CancellationToken.None);
        GameActionResult result = await ReadResultAsync(black);
        GameRuntimeState state = GamePersistenceService.Read(await database.ReadAsync());
        using AtariGoDbContext context = database.CreateContext();
        Move persisted = await context.Moves.SingleAsync();
        // Assert
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(StoneColor.Black, state.Board.Board[1]);
        Assert.AreEqual(StoneColor.White, state.Board.CurrentTurn);
        Assert.AreEqual(database.Time.GetUtcNow().UtcDateTime.AddSeconds(30), state.DeadlineUtc);
        Assert.AreEqual(database.Time.GetUtcNow().UtcDateTime, persisted.PlayedAt);
    }

    [TestMethod]
    public async Task ProcessAsync_AcceptedRetryAfterExpiry_RestoresReceiptAndAdvancesClock()
    {
        // Arrange
        GameActionLookupInterceptor interceptor = new GameActionLookupInterceptor();
        await using GameplayTestDatabase database = await GameplayTestDatabase.CreateAsync(
            interceptor);
        GameSessionConnection black = await database.ConnectAsync("Black");
        await database.ConnectAsync("White");
        GameActionCommand move = await database.CreateMoveAsync(new BoardPosition(0, 1));
        await database.Coordinator.ProcessAsync(black, move, CancellationToken.None);
        GameActionResult accepted = await ReadResultAsync(black);
        interceptor.LookupCallback = () => database.Time.Advance(TimeSpan.FromSeconds(31));
        // Act
        await database.Coordinator.ProcessAsync(black, move, CancellationToken.None);
        GameActionResult repeated = await ReadResultAsync(black);
        Game progress = await database.ReadAsync();
        GameRuntimeState state = GamePersistenceService.Read(progress);
        using AtariGoDbContext context = database.CreateContext();
        // Assert
        Assert.IsTrue(accepted.IsAccepted);
        Assert.AreEqual(accepted, repeated);
        Assert.AreEqual(accepted.Version + 1, progress.StateVersion);
        Assert.AreEqual(StoneColor.Black, state.Board.CurrentTurn);
        Assert.AreEqual(3, state.Board.TurnNumber);
        Assert.AreEqual(1, await context.Moves.CountAsync());
        Assert.AreEqual(1, await context.GameEvents.CountAsync(entry => entry.ActionId != null));
    }

    private static async Task PlayAsync(GameplayTestDatabase database,
        GameSessionConnection connection, BoardPosition position)
    {
        GameActionCommand command = await database.CreateMoveAsync(position);
        await database.Coordinator.ProcessAsync(connection, command, CancellationToken.None);
    }

    private static async Task<GameActionResult> ReadResultAsync(GameSessionConnection connection)
    {
        using CancellationTokenSource timeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(5));
        while (true)
        {
            GameSessionDelivery delivery = await connection.Events.ReadAsync(timeout.Token);
            if (delivery.ActionResult is not null)
            {
                return delivery.ActionResult;
            }
        }
    }
}
