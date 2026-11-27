using Microsoft.EntityFrameworkCore;

using AtariGo.Server.Data;

namespace AtariGo.Server.Tests.Games;

[TestClass]
[TestCategory("Integration")]
[TestCategory("SqlServer")]
public sealed class GameplaySchemaTests
{
    [TestMethod]
    public async Task Create_TextColumns_MatchAgreedStorageLimitsAsync()
    {
        await using SqlServerTestDatabase database = await SqlServerTestDatabase.CreateAsync();
        await using AtariGoDbContext context = database.CreateContext();
        (string Table, string Column, string Type, int Bytes)[] columns =
        {
            ("dbo.Profiles", "Description", "nvarchar", 400),
            ("dbo.Reports", "Description", "nvarchar", 400),
            ("dbo.GameEvents", "Description", "nvarchar", 400),
            ("dbo.GamePlayers", "GuestName", "nvarchar", 42),
            ("dbo.Rooms", "InvitationCode", "varchar", 10),
            ("dbo.Sessions", "SessionTokenHash", "char", 64),
            ("dbo.AuthenticationCodes", "CodeHash", "char", 64),
            ("dbo.Credentials", "PasswordHash", "varchar", 255),
            ("dbo.Users", "UserName", "nvarchar", 30),
            ("dbo.Users", "Email", "nvarchar", 508),
            ("dbo.Invitations", "RecipientEmail", "nvarchar", 508)
        };
        foreach ((string table, string column, string type, int bytes) in columns)
        {
            int matches = await context.Database.SqlQuery<int>($"""
                SELECT COUNT(*) AS Value FROM sys.columns
                WHERE object_id = OBJECT_ID({table}) AND name = {column}
                    AND max_length = {bytes} AND TYPE_NAME(user_type_id) = {type}
                """).SingleAsync();
            Assert.AreEqual(1, matches, table + "." + column);
        }
    }

    [TestMethod]
    public async Task Create_CurrentSchema_CreatesRelationalTablesWithoutLegacyColumnsAsync()
    {
        await using SqlServerTestDatabase database = await SqlServerTestDatabase.CreateAsync();
        await using AtariGoDbContext context = database.CreateContext();

        int obsolete = await context.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS Value FROM sys.columns WHERE object_id IN "
                + "(OBJECT_ID('dbo.Games'), OBJECT_ID('dbo.GameEvents')) AND name IN "
                + "('BoardStateJson', 'ActionRequestJson', 'ActionResultJson', 'ActionId', "
                + "'CurrentTurnGamePlayerId', 'TurnDeadlineUtc')").SingleAsync();

        Assert.AreEqual(0, obsolete);
        Assert.AreEqual(0, await context.Games.CountAsync());
        Assert.AreEqual(0, await context.Moves.CountAsync());
        Assert.AreEqual(0, await context.GamePlayers.CountAsync());
        Assert.AreEqual(0, await context.GameEvents.CountAsync());
    }
}
