using Microsoft.EntityFrameworkCore;

using AtariGo.Server.Data;

namespace AtariGo.Server.Tests.Games;

[TestClass]
[TestCategory("Integration")]
[TestCategory("SqlServer")]
public sealed class GameplaySchemaTests
{
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
