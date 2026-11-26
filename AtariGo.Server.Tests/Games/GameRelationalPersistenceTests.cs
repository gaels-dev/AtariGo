using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using AtariGo.GameLogic.Models;
using AtariGo.GameLogic.Rules;
using AtariGo.Server.Application.Games;
using AtariGo.Server.Data;
using AtariGo.Server.Data.Entities;

namespace AtariGo.Server.Tests.Games;

[TestClass]
[TestCategory("Integration")]
public sealed class GameRelationalPersistenceTests
{
    [TestMethod]
    public async Task ReadAsync_CompletedCapture_ReconstructsWithoutRuntimeOrJsonAsync()
    {
        await using GameplayTestDatabase database = await GameplayTestDatabase.CreateAsync();
        GameSessionConnection black = await database.ConnectAsync("Black");
        GameSessionConnection white = await database.ConnectAsync("White");
        await PlayAsync(database, black, new BoardPosition(0, 1));
        await PlayAsync(database, white, new BoardPosition(0, 0));
        await PlayAsync(database, black, new BoardPosition(1, 0));
        GameRuntimeState live = await database.ReadProgressAsync();
        await using AtariGoDbContext context = database.CreateContext();
        GamePersistenceService store = CreateFreshStore(database, context);

        Game game = await store.LoadAsync(database.GameId, CancellationToken.None);
        GameRuntimeState replayed = await store.ReadAsync(game, CancellationToken.None);

        CollectionAssert.AreEqual(live.Board.Board, replayed.Board.Board);
        Assert.AreEqual(StoneColor.Empty, replayed.Board.Board[0]);
        Assert.AreEqual(1, replayed.Board.BlackCaptures);
        Assert.AreEqual(StoneColor.Black, replayed.Board.Winner);
        Assert.AreEqual(GameRuntimeState.Completed, replayed.Status);
        Assert.AreEqual(3, await context.Moves.CountAsync());
    }

    [TestMethod]
    public async Task CloseInterruptedGamesAsync_Restart_CancelsOnceAndPreservesMovesAsync()
    {
        await using GameplayTestDatabase database = await GameplayTestDatabase.CreateAsync();
        GameSessionConnection black = await database.ConnectAsync("Black");
        await database.ConnectAsync("White");
        await PlayAsync(database, black, new BoardPosition(0, 1));
        await using AtariGoDbContext context = database.CreateContext();
        GamePersistenceService store = CreateFreshStore(database, context);

        await store.CloseInterruptedGamesAsync(CancellationToken.None);
        await store.CloseInterruptedGamesAsync(CancellationToken.None);
        Game game = await store.LoadAsync(database.GameId, CancellationToken.None);
        GameRuntimeState replayed = await store.ReadAsync(game, CancellationToken.None);

        Assert.AreEqual(GameRuntimeState.Cancelled, game.State);
        Assert.AreEqual("ServerRestart", game.EndReason);
        Assert.IsNotNull(game.FinishedAt);
        foreach (GamePlayer player in game.GamePlayers)
        {
            Assert.AreEqual("Abandoned", player.Result);
        }
        Assert.AreEqual(StoneColor.Empty, replayed.Board.Winner);
        Assert.AreEqual(StoneColor.Black, replayed.Board.Board[1]);
        Assert.AreEqual(1, await context.Moves.CountAsync());
        Assert.AreEqual(1, await context.GameEvents.CountAsync(
            entry => entry.EventType == "GameFinished"));
    }

    [TestMethod]
    public async Task CloseInterruptedGamesAsync_ActiveRuntime_PreservesGameAsync()
    {
        await using GameplayTestDatabase database = await GameplayTestDatabase.CreateAsync();
        await using AtariGoDbContext context = database.CreateContext();
        GamePersistenceService store = new GamePersistenceService(context, database.Time,
            database.Registry, new GameReplayService(new GameRulesEngine()),
            NullLogger<GamePersistenceService>.Instance);

        await store.CloseInterruptedGamesAsync(CancellationToken.None);

        Game game = await store.LoadAsync(database.GameId, CancellationToken.None);
        Assert.AreEqual(GameRuntimeState.Waiting, game.State);
        Assert.IsNull(game.FinishedAt);
    }

    private static GamePersistenceService CreateFreshStore(GameplayTestDatabase database,
        AtariGoDbContext context)
    {
        return new GamePersistenceService(context, database.Time,
            new GameSessionRegistry(database.Time), new GameReplayService(new GameRulesEngine()),
            NullLogger<GamePersistenceService>.Instance);
    }

    private static async Task PlayAsync(GameplayTestDatabase database,
        GameSessionConnection connection, BoardPosition position)
    {
        await database.AcknowledgeTurnAsync(connection);
        GameActionCommand command = await database.CreateMoveAsync(position);
        await database.Coordinator.ProcessAsync(connection, command, CancellationToken.None);
    }
}
