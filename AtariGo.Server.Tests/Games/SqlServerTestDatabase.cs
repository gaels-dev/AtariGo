using System.Text.RegularExpressions;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

using AtariGo.Server.Data;

namespace AtariGo.Server.Tests.Games;

internal sealed class SqlServerTestDatabase : IAsyncDisposable
{
    private const int SchemaCommandTimeoutSeconds = 120;
    private const string MasterConnection = @"Server=(localdb)\MSSQLLocalDB;Database=master;"
        + "Integrated Security=true;TrustServerCertificate=true;Connect Timeout=10";
    private readonly string _databaseName = "AtariGoGameplayTest_" + Guid.NewGuid().ToString("N");

    public string ConnectionString => new SqlConnectionStringBuilder(MasterConnection)
    {
        InitialCatalog = _databaseName
    }.ConnectionString;

    public static async Task<SqlServerTestDatabase> CreateAsync()
    {
        SqlServerTestDatabase database = new SqlServerTestDatabase();
        try
        {
            string create = await File.ReadAllTextAsync(ScriptPath("Create-AtariGo.sql"));
            await database.ExecuteAsync(create.Replace("AtariGoDB", database._databaseName), true);
            return database;
        }
        catch
        {
            await database.DisposeAsync();
            throw;
        }
    }

    public AtariGoDbContext CreateContext()
    {
        DbContextOptions<AtariGoDbContext> options = new DbContextOptionsBuilder<AtariGoDbContext>()
            .UseSqlServer(ConnectionString).Options;
        return new AtariGoDbContext(options);
    }

    public async Task UpgradeAsync()
    {
        await ExecuteAsync(await File.ReadAllTextAsync(ScriptPath("Update-Gameplay.sql")), false);
    }

    public async Task RestoreLegacySchemaAsync()
    {
        await ExecuteAsync("""
            DROP INDEX UX_GameEvents_Player_Action ON dbo.GameEvents;
            ALTER TABLE dbo.GameEvents DROP CONSTRAINT CK_GameEvents_Action;
            ALTER TABLE dbo.GameEvents DROP COLUMN ActionId, ActionRequestJson, ActionResultJson;
            DROP INDEX IX_Games_State_TurnDeadline ON dbo.Games;
            ALTER TABLE dbo.Games DROP CONSTRAINT CK_Games_StateVersion,
                CK_Games_BoardStateJson, CK_Games_GameplayState, DF_Games_StateVersion;
            ALTER TABLE dbo.Games DROP COLUMN BoardStateJson, StateVersion, TurnDeadlineUtc;
            UPDATE dbo.Games SET State = 'InProgress' WHERE State = 'Waiting';
            UPDATE dbo.GameConfigurations SET GameTimeSeconds = 600;
            ALTER TABLE dbo.GameConfigurations DROP CONSTRAINT CK_GameConfigurations_GameTime;
            ALTER TABLE dbo.GameConfigurations ADD CONSTRAINT CK_GameConfigurations_GameTime
                CHECK (GameTimeSeconds > 0);
            ALTER TABLE dbo.GameConfigurations DROP CONSTRAINT CK_GameConfigurations_TurnTime;
            ALTER TABLE dbo.GameConfigurations ADD CONSTRAINT CK_GameConfigurations_TurnTime
                CHECK (TurnTimeSeconds > 0 AND TurnTimeSeconds <= GameTimeSeconds);
            """, false);
    }

    public async Task RestorePreviousDeploymentAsync()
    {
        // Test fixture only: reproduce the schema already deployed before the correction.
        await ExecuteAsync("""
            CREATE TABLE dbo.GameProgresses
            (
                GameId BIGINT NOT NULL PRIMARY KEY REFERENCES dbo.Games(GameId),
                Version BIGINT NOT NULL, StateJson NVARCHAR(MAX) NOT NULL,
                IsTerminal BIT NOT NULL, NextDeadlineUtc DATETIME2(3) NOT NULL
            );
            CREATE TABLE dbo.GameActionReceipts
            (
                GameId BIGINT NOT NULL REFERENCES dbo.Games(GameId),
                PlayerKey VARCHAR(64) NOT NULL, ActionId VARCHAR(64) NOT NULL,
                RequestJson NVARCHAR(MAX) NOT NULL, Rejection VARCHAR(40) NOT NULL,
                Version BIGINT NOT NULL, IsAccepted BIT NOT NULL,
                PRIMARY KEY (GameId, PlayerKey, ActionId)
            );
            INSERT INTO dbo.GameProgresses
            SELECT game.GameId, game.StateVersion,
                (SELECT JSON_QUERY(game.BoardStateJson) AS Board, game.State AS Status,
                    CONCAT(CONVERT(VARCHAR(23), game.TurnDeadlineUtc, 126), 'Z') AS DeadlineUtc,
                    COALESCE(game.EndReason, '') AS EndReason FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
                CASE WHEN game.State IN ('Completed', 'Cancelled') THEN 1 ELSE 0 END,
                game.TurnDeadlineUtc
            FROM dbo.Games AS game WHERE game.BoardStateJson IS NOT NULL;
            INSERT INTO dbo.GameActionReceipts
            SELECT history.GameId,
                CASE WHEN player.UserId IS NOT NULL THEN CONCAT('user:', player.UserId)
                ELSE CONCAT('guest:', LOWER(REPLACE(CONVERT(VARCHAR(36),
                    player.GuestIdentityId), '-', ''))) END,
                history.ActionId, history.ActionRequestJson,
                JSON_VALUE(history.ActionResultJson, '$.Rejection'),
                CONVERT(BIGINT, JSON_VALUE(history.ActionResultJson, '$.Version')),
                CASE WHEN JSON_VALUE(history.ActionResultJson, '$.IsAccepted') = 'true'
                    THEN 1 ELSE 0 END
            FROM dbo.GameEvents AS history JOIN dbo.GamePlayers AS player
                ON player.GamePlayerId = history.GamePlayerId WHERE history.ActionId IS NOT NULL;
            UPDATE dbo.GameEvents SET Description = ActionId WHERE ActionId IS NOT NULL;
            DELETE FROM dbo.GameEvents WHERE EventType = 'ActionRejected';
            """, false);
        await RestoreLegacySchemaAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (!Regex.IsMatch(_databaseName, @"^AtariGoGameplayTest_[a-f0-9]{32}$"))
        {
            throw new InvalidOperationException("Only the isolated test database can be removed.");
        }
        SqlConnection.ClearAllPools();
        await using SqlConnection connection = new SqlConnection(MasterConnection);
        await connection.OpenAsync();
        string sql = $"IF DB_ID(N'{_databaseName}') IS NOT NULL BEGIN "
            + $"ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; "
            + $"DROP DATABASE [{_databaseName}]; END;";
        await using SqlCommand command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private async Task ExecuteAsync(string script, bool master)
    {
        await using SqlConnection connection = new SqlConnection(
            master ? MasterConnection : ConnectionString);
        await connection.OpenAsync();
        foreach (string batch in Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline))
        {
            if (!string.IsNullOrWhiteSpace(batch))
            {
                await using SqlCommand command = new SqlCommand(batch, connection)
                {
                    CommandTimeout = SchemaCommandTimeoutSeconds
                };
                await command.ExecuteNonQueryAsync();
            }
        }
    }

    private static string ScriptPath(string file)
    {
        return Path.Combine(AppContext.BaseDirectory, "DatabaseScripts", file);
    }
}
