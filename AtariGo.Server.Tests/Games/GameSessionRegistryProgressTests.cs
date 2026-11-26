using AtariGo.Server.Application.Games;
using AtariGo.Server.Data.Entities;

namespace AtariGo.Server.Tests.Games;

[TestClass]
[TestCategory("Unit")]
public sealed class GameSessionRegistryProgressTests
{
    [TestMethod]
    public void RememberAction_CapacityReached_RetainsNewestReceiptsAndDetectsConflict()
    {
        GameSessionRegistry registry = new GameSessionRegistry(TimeProvider.System);
        Game game = new Game { GameId = 1 };
        GamePlayer player = new GamePlayer { GamePlayerId = 2 };
        registry.StoreProgress(1, GameRuntimeState.CreateInitial(1, DateTime.UtcNow), 0);
        GameActionContext first = CreateContext(game, player, "first");
        registry.RememberAction(first, new GameActionResult { ActionId = "first" });
        for (int index = 0; index < GameSessionRegistry.MaximumActionReceipts; index++)
        {
            GameActionContext current = CreateContext(game, player, index.ToString());
            registry.RememberAction(current, new GameActionResult
            {
                ActionId = current.Command.ActionId,
                IsAccepted = true
            });
        }
        GameActionContext last = CreateContext(game, player,
            (GameSessionRegistry.MaximumActionReceipts - 1).ToString());

        GameActionResult? retained = registry.FindAction(last);
        GameActionContext conflict = new GameActionContext
        {
            Game = game,
            Player = player,
            Command = last.Command with { Column = 1 }
        };

        Assert.IsNull(registry.FindAction(first));
        Assert.IsTrue(retained!.IsAccepted);
        Assert.AreEqual("ActionConflict", registry.FindAction(conflict)!.Rejection);
    }

    private static GameActionContext CreateContext(Game game, GamePlayer player, string actionId)
    {
        return new GameActionContext
        {
            Game = game,
            Player = player,
            Command = new GameActionCommand { ActionId = actionId }
        };
    }
}
