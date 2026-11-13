using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using AtariGo.GameLogic.Rules;
using AtariGo.Server.Application.Games;
using AtariGo.Server.Configuration;
using AtariGo.Server.Data;
using AtariGo.Server.Data.Entities;

namespace AtariGo.Server.Tests.Games;

[TestClass]
[TestCategory("Integration")]
[TestCategory("SqlServer")]
public sealed class SqlServerGameplayTests
{
    [TestMethod]
    public async Task Scripts_FreshSchemaAndRepeatedUpgrade_PreserveGameAndAcceptMoves()
    {
        // Arrange
        await using SqlServerTestDatabase database = await SqlServerTestDatabase.CreateAsync();
        ServiceCollection services = CreateServices(database.ConnectionString);
        await using ServiceProvider provider = services.BuildServiceProvider();
        Guid blackId = Guid.NewGuid();
        Guid whiteId = Guid.NewGuid();
        long gameId = await SeedAsync(provider, (blackId, whiteId));
        // Act
        await database.UpgradeAsync();
        await database.UpgradeAsync();
        GameCoordinator coordinator = provider.GetRequiredService<GameCoordinator>();
        GameSessionParticipant blackPlayer = new GameSessionParticipant(
            GameSessionRules.CreateGuestKey(blackId), "Black", "Black");
        GameSessionParticipant whitePlayer = new GameSessionParticipant(
            GameSessionRules.CreateGuestKey(whiteId), "White", "White");
        GameSessionConnection black = await coordinator.ConnectAsync(new GameSessionDefinition
        {
            GameId = gameId, CaptureGoal = 1, TurnTimeSeconds = 30,
            Player = blackPlayer, Opponent = whitePlayer
        }, CancellationToken.None);
        await coordinator.ConnectAsync(new GameSessionDefinition
        {
            GameId = gameId, CaptureGoal = 1, TurnTimeSeconds = 30,
            Player = whitePlayer, Opponent = blackPlayer
        }, CancellationToken.None);
        await using AtariGoDbContext read = database.CreateContext();
        long version = (await read.Games.AsNoTracking().SingleAsync()).StateVersion;
        await coordinator.ProcessAsync(black, new GameActionCommand
        {
            ActionId = Guid.NewGuid().ToString("N"), ExpectedVersion = version, Row = 0, Column = 1
        }, CancellationToken.None);
        // Assert
        Assert.AreEqual(1, await read.Games.CountAsync());
        Assert.AreEqual(1, await read.Moves.CountAsync());
        Assert.AreEqual(1, await read.GameEvents.CountAsync(entry => entry.ActionId != null));
        Assert.IsTrue(await new DatabaseReadinessCheck(read).CheckAsync(
            CancellationToken.None) > 0);
    }

    [TestMethod]
    public async Task Scripts_LegacyUnplayedGame_BackfillsWithoutDeletingParticipants()
    {
        // Arrange
        await using SqlServerTestDatabase database = await SqlServerTestDatabase.CreateAsync();
        await using ServiceProvider provider = CreateServices(database.ConnectionString)
            .BuildServiceProvider();
        long gameId = await SeedAsync(provider, (Guid.NewGuid(), Guid.NewGuid()));
        await database.RestoreLegacySchemaAsync();
        // Act
        await database.UpgradeAsync();
        await database.UpgradeAsync();
        await using AtariGoDbContext context = database.CreateContext();
        Game progress = await context.Games.SingleAsync();
        // Assert
        Assert.AreEqual(gameId, progress.GameId);
        Assert.AreEqual(2, await context.GamePlayers.CountAsync());
        Assert.AreEqual(GameRuntimeState.Waiting, GamePersistenceService.Read(progress).Status);
        Assert.AreEqual(1, GamePersistenceService.Read(progress).Board.CaptureGoal);
    }

    [TestMethod]
    public async Task Scripts_LegacyActiveGameWithMoves_RollsBackWithoutResettingBoardHistory()
    {
        // Arrange
        await using SqlServerTestDatabase database = await SqlServerTestDatabase.CreateAsync();
        await using ServiceProvider provider = CreateServices(database.ConnectionString)
            .BuildServiceProvider();
        await SeedAsync(provider, (Guid.NewGuid(), Guid.NewGuid()));
        await using AtariGoDbContext context = database.CreateContext();
        long playerId = await context.GamePlayers
            .Select(player => player.GamePlayerId).FirstAsync();
        context.Moves.Add(new Move
        {
            GamePlayerId = playerId, TurnNumber = 1, Row = 0, Column = 0, PlayedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        await database.RestoreLegacySchemaAsync();
        // Act
        SqlException exception = await Assert.ThrowsAsync<SqlException>(database.UpgradeAsync);
        // Assert
        Assert.AreEqual(51001, exception.Number);
        Assert.AreEqual(1, await context.Moves.CountAsync());
        int stateColumns = await context.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS Value FROM sys.columns "
                + "WHERE object_id = OBJECT_ID('dbo.Games') AND name = 'BoardStateJson'")
            .SingleAsync();
        Assert.AreEqual(0, stateColumns);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Scripts_PreviousDeployment_MigratesBoardAndResponsesWithoutDuplicateEvents(
        bool finishBySurrender)
    {
        // Arrange
        await using SqlServerTestDatabase database = await SqlServerTestDatabase.CreateAsync();
        await using ServiceProvider before = CreateServices(database.ConnectionString)
            .BuildServiceProvider();
        (Guid Black, Guid White) ids = (Guid.NewGuid(), Guid.NewGuid());
        long gameId = await SeedAsync(before, ids);
        (GameSessionConnection Black, GameSessionConnection White) players =
            await ConnectPlayersAsync(before, gameId, ids);
        GameActionCommand move = new GameActionCommand
        {
            ActionId = Guid.NewGuid().ToString("N"), ExpectedVersion = 1, Row = 0, Column = 1
        };
        GameActionCommand rejected = move with
        {
            ActionId = Guid.NewGuid().ToString("N"), ExpectedVersion = 2
        };
        GameCoordinator coordinator = before.GetRequiredService<GameCoordinator>();
        await coordinator.ProcessAsync(players.Black, move, CancellationToken.None);
        await coordinator.ProcessAsync(players.White, rejected, CancellationToken.None);
        if (finishBySurrender)
        {
            await coordinator.ProcessAsync(players.White, new GameActionCommand
            {
                ActionId = Guid.NewGuid().ToString("N"), IsSurrender = true
            }, CancellationToken.None);
        }
        await using AtariGoDbContext context = database.CreateContext();
        Game original = await context.Games.AsNoTracking().SingleAsync();
        GameEvent originalEvent = await context.GameEvents.AsNoTracking()
            .SingleAsync(entry => entry.ActionId == move.ActionId);
        await database.RestorePreviousDeploymentAsync();
        // Act
        await database.UpgradeAsync();
        await database.UpgradeAsync();
        await using ServiceProvider after = CreateServices(database.ConnectionString)
            .BuildServiceProvider();
        (GameSessionConnection Black, GameSessionConnection White) restored =
            await ConnectPlayersAsync(after, gameId, ids);
        await after.GetRequiredService<GameCoordinator>().ProcessAsync(
            restored.Black, move, CancellationToken.None);
        await after.GetRequiredService<GameCoordinator>().ProcessAsync(
            restored.White, rejected, CancellationToken.None);
        // Assert
        Game migrated = await context.Games.AsNoTracking().SingleAsync();
        AssertMigratedState(original, migrated);
        Assert.AreEqual(1, await context.Moves.CountAsync());
        int expectedActions = finishBySurrender ? 3 : 2;
        Assert.AreEqual(expectedActions,
            await context.GameEvents.CountAsync(entry => entry.ActionId != null));
        GameEvent reused = await context.GameEvents.AsNoTracking()
            .SingleAsync(entry => entry.ActionId == move.ActionId);
        Assert.AreEqual(originalEvent.GameEventId, reused.GameEventId);
        Assert.AreEqual(originalEvent.CreatedAt, reused.CreatedAt);
        Assert.IsTrue((await ReadActionAsync(restored.Black)).IsAccepted);
        Assert.AreEqual("Occupied", (await ReadActionAsync(restored.White)).Rejection);
        Assert.AreEqual(0, await CountDiscardedTablesAsync(context));
    }

    [TestMethod]
    public async Task Scripts_UnlinkedSavedAction_RollsBackAndPreservesPreviousTables()
    {
        // Arrange
        await using SqlServerTestDatabase database = await SqlServerTestDatabase.CreateAsync();
        await using ServiceProvider provider = CreateServices(database.ConnectionString)
            .BuildServiceProvider();
        (Guid Black, Guid White) ids = (Guid.NewGuid(), Guid.NewGuid());
        long gameId = await SeedAsync(provider, ids);
        (GameSessionConnection Black, GameSessionConnection White) players =
            await ConnectPlayersAsync(provider, gameId, ids);
        await provider.GetRequiredService<GameCoordinator>().ProcessAsync(players.Black,
            new GameActionCommand
            {
                ActionId = Guid.NewGuid().ToString("N"), ExpectedVersion = 1, Row = 0, Column = 0
            }, CancellationToken.None);
        await database.RestorePreviousDeploymentAsync();
        await using AtariGoDbContext context = database.CreateContext();
        await context.Database.ExecuteSqlRawAsync(
            "UPDATE dbo.GameActionReceipts SET PlayerKey = 'user:999999';");
        // Act
        SqlException exception = await Assert.ThrowsAsync<SqlException>(database.UpgradeAsync);
        // Assert
        Assert.AreEqual(51005, exception.Number);
        Assert.AreEqual(2, await CountDiscardedTablesAsync(context));
        Assert.AreEqual(1, await context.Moves.CountAsync());
        int columns = await context.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS Value FROM sys.columns "
                + "WHERE object_id = OBJECT_ID('dbo.Games') AND name = 'BoardStateJson'")
            .SingleAsync();
        Assert.AreEqual(0, columns);
    }

    private static void AssertMigratedState(Game original, Game migrated)
    {
        Assert.AreEqual(original.BoardStateJson, migrated.BoardStateJson);
        Assert.AreEqual(original.StateVersion, migrated.StateVersion);
        Assert.AreEqual(original.TurnDeadlineUtc, migrated.TurnDeadlineUtc);
        Assert.AreEqual(original.State, migrated.State);
        Assert.AreEqual(original.EndReason, migrated.EndReason);
        Assert.AreEqual(original.FinishedAt, migrated.FinishedAt);
        Assert.AreEqual(original.CurrentTurnGamePlayerId, migrated.CurrentTurnGamePlayerId);
    }

    private static async Task<int> CountDiscardedTablesAsync(AtariGoDbContext context)
    {
        return await context.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS Value FROM sys.tables "
                + "WHERE name IN ('GameProgresses', 'GameActionReceipts')").SingleAsync();
    }

    private static async Task<GameActionResult> ReadActionAsync(GameSessionConnection connection)
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

    private static async Task<(GameSessionConnection Black, GameSessionConnection White)>
        ConnectPlayersAsync(ServiceProvider provider, long gameId, (Guid Black, Guid White) ids)
    {
        GameCoordinator coordinator = provider.GetRequiredService<GameCoordinator>();
        GameSessionParticipant black = new GameSessionParticipant(
            GameSessionRules.CreateGuestKey(ids.Black), "Black", "Black");
        GameSessionParticipant white = new GameSessionParticipant(
            GameSessionRules.CreateGuestKey(ids.White), "White", "White");
        GameSessionDefinition definition = new GameSessionDefinition
        {
            GameId = gameId, CaptureGoal = 1, TurnTimeSeconds = 30,
            Player = black, Opponent = white
        };
        GameSessionConnection blackConnection = await coordinator.ConnectAsync(
            definition, CancellationToken.None);
        GameSessionConnection whiteConnection = await coordinator.ConnectAsync(
            definition with { Player = white, Opponent = black }, CancellationToken.None);
        return (blackConnection, whiteConnection);
    }

    private static ServiceCollection CreateServices(string connectionString)
    {
        ServiceCollection services = new ServiceCollection();
        services.AddDbContext<AtariGoDbContext>(options => options.UseSqlServer(connectionString));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IGameSessionRegistry, GameSessionRegistry>();
        services.AddSingleton<GameRulesEngine>();
        services.AddSingleton<GameCoordinator>();
        services.AddScoped<GamePersistenceService>();
        services.AddScoped<GameActionProcessor>();
        return services;
    }

    private static async Task<long> SeedAsync(ServiceProvider provider, (Guid Black,
        Guid White) ids)
    {
        using IServiceScope scope = provider.CreateScope();
        AtariGoDbContext context = scope.ServiceProvider.GetRequiredService<AtariGoDbContext>();
        DateTime now = DateTime.UtcNow;
        Game game = new Game
        {
            GameType = "Public", State = "InProgress", StartedAt = now,
            GameConfiguration = new GameConfiguration { CaptureGoal = 1, TurnTimeSeconds = 30 }
        };
        game.GamePlayers.Add(new GamePlayer
        {
            GuestIdentityId = ids.Black, GuestName = "Black", StoneColor = "Black",
            TurnOrder = 1, JoinedAt = now, Result = "Pending"
        });
        game.GamePlayers.Add(new GamePlayer
        {
            GuestIdentityId = ids.White, GuestName = "White", StoneColor = "White",
            TurnOrder = 2, JoinedAt = now, Result = "Pending"
        });
        GamePersistenceService.Initialize(game, 1, now);
        context.Games.Add(game);
        await context.SaveChangesAsync();
        return game.GameId;
    }
}
