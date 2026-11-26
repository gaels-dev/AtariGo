using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

using AtariGo.GameLogic.Models;
using AtariGo.GameLogic.Rules;
using AtariGo.Server.Application.Games;
using AtariGo.Server.Data;
using AtariGo.Server.Data.Entities;

namespace AtariGo.Server.Tests.Games;

[TestClass]
[TestCategory("Integration")]
[TestCategory("SqlServer")]
public sealed class RelationalGameplayMigrationTests
{
    [TestMethod]
    public async Task Update_LegacyGames_ReplacesFixturesWithReplayableCaptureAndRunsOnceAsync()
    {
        await using SqlServerTestDatabase database = await SqlServerTestDatabase.CreateAsync();
        await database.ExecuteSqlAsync(CreateLegacyColumns());
        await database.ExecuteSqlAsync(CreateGames());

        await database.ExecuteScriptAsync("Update-RelationalGameplay.sql");
        await using AtariGoDbContext context = database.CreateContext();
        List<Game> games = await context.Games.Include(game => game.GamePlayers)
            .Include(game => game.GameConfiguration).OrderBy(game => game.GameId).ToListAsync();
        GameReplayService replay = new GameReplayService(new GameRulesEngine());
        foreach (Game game in games)
        {
            List<Move> moves = await context.Moves
                .Where(move => move.GamePlayer.GameId == game.GameId)
                .OrderBy(move => move.TurnNumber).ToListAsync();
            GameState board = replay.Reconstruct(game, moves);
            Assert.HasCount(3, moves);
            Assert.AreEqual(StoneColor.Empty, board.Board[0]);
            Assert.AreEqual(StoneColor.Black, board.Board[1]);
            Assert.AreEqual(StoneColor.Black, board.Board[9]);
            Assert.AreEqual(1, board.BlackCaptures);
            Assert.AreEqual(StoneColor.Black, board.Winner);
            Assert.AreEqual("Completed", game.State);
            Assert.AreEqual("CaptureGoal", game.EndReason);
            foreach (GamePlayer player in game.GamePlayers)
            {
                AssertParticipant(player);
            }
        }
        long[] moveIds = await context.Moves.OrderBy(move => move.MoveId)
            .Select(move => move.MoveId).ToArrayAsync();
        await database.ExecuteScriptAsync("Update-RelationalGameplay.sql");
        long[] unchangedIds = await context.Moves.OrderBy(move => move.MoveId)
            .Select(move => move.MoveId).ToArrayAsync();
        CollectionAssert.AreEqual(moveIds, unchangedIds);
        Assert.HasCount(2, games);
        Assert.AreEqual(4, await context.GameEvents.CountAsync());
        await AssertRelationalColumnsAsync(context);
    }

    [TestMethod]
    public async Task Update_InvalidParticipants_RollsBackWithoutRemovingLegacyColumnsAsync()
    {
        await using SqlServerTestDatabase database = await SqlServerTestDatabase.CreateAsync();
        await database.ExecuteSqlAsync(CreateLegacyColumns());
        await database.ExecuteSqlAsync(CreateGames());
        await database.ExecuteSqlAsync("DELETE FROM dbo.GamePlayers WHERE GamePlayerId = 1;");

        await Assert.ThrowsAsync<SqlException>(
            () => database.ExecuteScriptAsync("Update-RelationalGameplay.sql"));

        await using AtariGoDbContext context = database.CreateContext();
        int columns = await context.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS Value FROM sys.columns "
                + "WHERE object_id = OBJECT_ID('dbo.Games') AND name = 'BoardStateJson'")
            .SingleAsync();
        Assert.AreEqual(1, columns);
        Assert.AreEqual(3, await context.GamePlayers.CountAsync());
    }

    [TestMethod]
    public async Task Create_CurrentSchema_DoesNotRequireMigrationOrJsonColumnsAsync()
    {
        await using SqlServerTestDatabase database = await SqlServerTestDatabase.CreateAsync();

        await database.ExecuteScriptAsync("Update-RelationalGameplay.sql");

        await using AtariGoDbContext context = database.CreateContext();
        await AssertRelationalColumnsAsync(context);
        Assert.AreEqual(0, await context.Games.CountAsync());
    }

    private static void AssertParticipant(GamePlayer player)
    {
        if (player.StoneColor == "Black")
        {
            Assert.AreEqual("Won", player.Result);
            Assert.AreEqual(1, player.CapturedStones);
        }
        else
        {
            Assert.AreEqual("Lost", player.Result);
            Assert.AreEqual(0, player.CapturedStones);
        }
    }

    private static async Task AssertRelationalColumnsAsync(AtariGoDbContext context)
    {
        int obsolete = await context.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS Value FROM sys.columns WHERE object_id IN "
                + "(OBJECT_ID('dbo.Games'), OBJECT_ID('dbo.GameEvents')) AND name IN "
                + "('BoardStateJson', 'ActionRequestJson', 'ActionResultJson', 'ActionId', "
                + "'CurrentTurnGamePlayerId', 'TurnDeadlineUtc')").SingleAsync();
        Assert.AreEqual(0, obsolete);
    }

    private static string CreateLegacyColumns()
    {
        return """
            ALTER TABLE dbo.Games ADD BoardStateJson NVARCHAR(MAX) NULL,
                TurnDeadlineUtc DATETIME2(3) NULL, CurrentTurnGamePlayerId BIGINT NULL;
            ALTER TABLE dbo.GameEvents ADD ActionId VARCHAR(64) NULL,
                ActionRequestJson NVARCHAR(MAX) NULL, ActionResultJson NVARCHAR(MAX) NULL;
            GO
            ALTER TABLE dbo.Games ADD CONSTRAINT CK_Games_BoardStateJson
                CHECK (BoardStateJson IS NULL OR ISJSON(BoardStateJson) = 1);
            ALTER TABLE dbo.Games ADD CONSTRAINT CK_Games_GameplayState
                CHECK ((BoardStateJson IS NULL AND TurnDeadlineUtc IS NULL)
                    OR (BoardStateJson IS NOT NULL AND TurnDeadlineUtc IS NOT NULL));
            ALTER TABLE dbo.Games ADD CONSTRAINT FK_Games_CurrentTurnPlayer
                FOREIGN KEY (GameId, CurrentTurnGamePlayerId)
                REFERENCES dbo.GamePlayers (GameId, GamePlayerId);
            ALTER TABLE dbo.GameEvents ADD CONSTRAINT CK_GameEvents_Action
                CHECK (ActionId IS NULL OR ActionResultJson IS NOT NULL);
            CREATE INDEX IX_Games_State_TurnDeadline ON dbo.Games (State, TurnDeadlineUtc);
            CREATE UNIQUE INDEX UX_GameEvents_Player_Action
                ON dbo.GameEvents (GameId, GamePlayerId, ActionId) WHERE ActionId IS NOT NULL;
            """;
    }

    private static string CreateGames()
    {
        return """
            INSERT dbo.GameConfigurations (CaptureGoal, GameTimeSeconds, TurnTimeSeconds)
                VALUES (3, 0, 30), (5, 0, 60);
            INSERT dbo.Games (GameConfigurationId, GameType)
                SELECT GameConfigurationId, 'Public' FROM dbo.GameConfigurations;
            INSERT dbo.GamePlayers
                (GameId, GuestName, GuestIdentityId, StoneColor, TurnOrder)
                SELECT GameId, 'Black test guest', NEWID(), 'Black', 1 FROM dbo.Games
                UNION ALL
                SELECT GameId, 'White test guest', NEWID(), 'White', 2 FROM dbo.Games;
            """;
    }
}
