using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using AtariGo.GameLogic.Models;
using AtariGo.Server.Application.Games;
using AtariGo.Server.Data;
using AtariGo.Server.Data.Entities;

namespace AtariGo.Server.Tests.Games;

[TestClass]
[TestCategory("Integration")]
public sealed class GameCommandServiceTests
{
    [TestMethod]
    public async Task AdvanceAsync_WaitingWithOnePlayer_PreservesGraceDeadlineAndVersionAsync()
    {
        // Arrange
        await using GameplayTestDatabase database = await GameplayTestDatabase.CreateAsync();
        await database.ConnectAsync("Black");
        using IServiceScope scope = database.Services.CreateScope();
        GameCommandService commands = scope.ServiceProvider
            .GetRequiredService<GameCommandService>();
        GameOperationContext operation = await commands.LoadAsync(database.GameId,
            CancellationToken.None);
        // Act
        GameOperationResult result = await commands.AdvanceAsync(operation, () => false);
        // Assert
        Assert.AreEqual(GameRuntimeState.Waiting, result.Progress!.Status);
        Assert.AreEqual(operation.Progress.DeadlineUtc, result.Progress.DeadlineUtc);
        Assert.AreEqual(operation.Version, result.Version);
    }

    [TestMethod]
    public async Task AdvanceAsync_GraceExpired_DoesNotStartWhenBothPlayersAreConnectedAsync()
    {
        // Arrange
        await using GameplayTestDatabase database = await GameplayTestDatabase.CreateAsync();
        await database.ConnectAsync("Black");
        database.Time.Advance(TimeSpan.FromSeconds(GameRuntimeState.ConnectionGraceSeconds));
        using IServiceScope scope = database.Services.CreateScope();
        GameCommandService commands = scope.ServiceProvider
            .GetRequiredService<GameCommandService>();
        GameOperationContext operation = await commands.LoadAsync(database.GameId,
            CancellationToken.None);
        // Act
        GameOperationResult result = await commands.AdvanceAsync(operation, () => true);
        Game progress = await database.ReadAsync();
        // Assert
        Assert.AreEqual(GameRuntimeState.Cancelled, result.Progress!.Status);
        Assert.AreEqual("ConnectionTimeout", result.Progress.EndReason);
        Assert.IsNotNull(progress.FinishedAt);
        Assert.AreEqual(progress.StateVersion, result.Version);
    }

    [TestMethod]
    public async Task ProcessAsync_RepeatedAction_ReadsConnectionStateAfterReceiptLookupAsync()
    {
        // Arrange
        GameActionLookupInterceptor interceptor = new GameActionLookupInterceptor();
        await using GameplayTestDatabase database = await GameplayTestDatabase.CreateAsync(
            interceptor);
        GameSessionConnection black = await database.ConnectAsync("Black");
        GameActionCommand move = await database.CreateMoveAsync(new BoardPosition(0, 1));
        await database.Coordinator.ProcessAsync(black, move, CancellationToken.None);
        bool lookupCompleted = false;
        bool connectionReadAfterLookup = false;
        interceptor.LookupCallback = () => lookupCompleted = true;
        using IServiceScope scope = database.Services.CreateScope();
        GameCommandService commands = scope.ServiceProvider
            .GetRequiredService<GameCommandService>();
        // Act
        GameOperationResult result = await commands.ProcessAsync(black, move,
            () => connectionReadAfterLookup = lookupCompleted);
        // Assert
        Assert.IsTrue(connectionReadAfterLookup);
        Assert.AreEqual("Waiting", result.ActionResult!.Rejection);
    }

    [TestMethod]
    public async Task ProcessAsync_ValidMove_PersistsWithoutPublishingToConnectionsAsync()
    {
        // Arrange
        await using GameplayTestDatabase database = await GameplayTestDatabase.CreateAsync();
        GameSessionConnection black = await database.ConnectAsync("Black");
        await database.ConnectAsync("White");
        GameActionCommand move = await database.CreateMoveAsync(new BoardPosition(0, 1));
        DrainEvents(black);
        using IServiceScope scope = database.Services.CreateScope();
        GameCommandService commands = scope.ServiceProvider
            .GetRequiredService<GameCommandService>();
        // Act
        GameOperationResult result = await commands.ProcessAsync(black, move, () => true);
        Game progress = await database.ReadAsync();
        // Assert
        Assert.IsTrue(result.ActionResult!.IsAccepted);
        Assert.AreEqual(progress.StateVersion, result.Version);
        Assert.AreEqual(StoneColor.Black, result.Progress!.Board.Board[1]);
        Assert.IsFalse(black.Events.TryRead(out _));
        using AtariGoDbContext context = database.CreateContext();
        Assert.AreEqual(1, await context.Moves.CountAsync());
    }

    [TestMethod]
    public async Task
        ProcessAsync_ActionIdReusedForDifferentMove_ReturnsConflictWithoutNewMoveAsync()
    {
        // Arrange
        await using GameplayTestDatabase database = await GameplayTestDatabase.CreateAsync();
        GameSessionConnection black = await database.ConnectAsync("Black");
        await database.ConnectAsync("White");
        GameActionCommand move = await database.CreateMoveAsync(new BoardPosition(0, 1));
        await database.Coordinator.ProcessAsync(black, move, CancellationToken.None);
        using IServiceScope scope = database.Services.CreateScope();
        GameCommandService commands = scope.ServiceProvider
            .GetRequiredService<GameCommandService>();
        // Act
        GameOperationResult result = await commands.ProcessAsync(black,
            move with
            {
                Column = 2
            }, () => true);
        // Assert
        Assert.IsFalse(result.ActionResult!.IsAccepted);
        Assert.AreEqual("ActionConflict", result.ActionResult.Rejection);
        using AtariGoDbContext context = database.CreateContext();
        Assert.AreEqual(1, await context.Moves.CountAsync());
        Assert.AreEqual(0, await context.GameEvents.CountAsync(entry => entry.EventType == "Move"));
    }

    [TestMethod]
    public async Task ProcessAsync_InvalidActionId_ReturnsRejectionWithoutSavingAsync()
    {
        // Arrange
        await using GameplayTestDatabase database = await GameplayTestDatabase.CreateAsync();
        GameSessionConnection black = await database.ConnectAsync("Black");
        GameActionCommand move = new GameActionCommand { ActionId = "invalid" };
        using IServiceScope scope = database.Services.CreateScope();
        GameCommandService commands = scope.ServiceProvider
            .GetRequiredService<GameCommandService>();
        // Act
        GameOperationResult result = await commands.ProcessAsync(black, move, () => false);
        // Assert
        Assert.AreEqual("InvalidAction", result.ActionResult!.Rejection);
        Assert.IsNull(result.Progress);
        using AtariGoDbContext context = database.CreateContext();
        Assert.AreEqual(0, await context.Moves.CountAsync());
    }

    [TestMethod]
    public async Task ProcessAsync_MoveWriteFails_RollsBackAndDoesNotPublishAsync()
    {
        // Arrange
        GameWriteFailureInterceptor interceptor = new GameWriteFailureInterceptor();
        await using GameplayTestDatabase database = await GameplayTestDatabase.CreateAsync(
            interceptor);
        GameSessionConnection black = await database.ConnectAsync("Black");
        GameSessionConnection white = await database.ConnectAsync("White");
        GameActionCommand move = await database.CreateMoveAsync(new BoardPosition(0, 1));
        Game before = await database.ReadAsync();
        DrainEvents(black);
        DrainEvents(white);
        interceptor.FailMoveWrite = true;
        // Act
        await Assert.ThrowsAsync<DbUpdateException>(() => database.Coordinator.ProcessAsync(
            black, move, CancellationToken.None));
        Game after = await database.ReadAsync();
        // Assert
        Assert.AreEqual(before.StateVersion, after.StateVersion);
        Assert.IsTrue((await database.ReadProgressAsync()).Board.Board.All(IsEmpty));
        Assert.IsFalse(black.Events.TryRead(out _));
        Assert.IsFalse(white.Events.TryRead(out _));
        using AtariGoDbContext context = database.CreateContext();
        Assert.AreEqual(0, await context.Moves.CountAsync());
        interceptor.FailMoveWrite = false;
        await database.Coordinator.ProcessAsync(black, move, CancellationToken.None);
        Assert.AreEqual(1, await context.Moves.CountAsync());
        Assert.AreEqual(StoneColor.Black, (await database.ReadProgressAsync()).Board.Board[1]);
    }

    private static bool IsEmpty(StoneColor stone)
    {
        return stone == StoneColor.Empty;
    }

    private static void DrainEvents(GameSessionConnection connection)
    {
        while (connection.Events.TryRead(out GameSessionDelivery? delivery))
        {
            Assert.IsNotNull(delivery.Snapshot);
        }
    }
}
