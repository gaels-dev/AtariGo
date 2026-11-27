using AtariGo.GameLogic.Models;
using AtariGo.GameLogic.Rules;
using AtariGo.Server.Application.Games;
using AtariGo.Server.Data.Entities;

namespace AtariGo.Server.Tests.Games;

[TestClass]
[TestCategory("Unit")]
public sealed class GameReplayServiceTests
{
    [TestMethod]
    public void Reconstruct_SkippedTurns_RemovesCapturedStoneAndPreservesRepetitionHistory()
    {
        Game game = CreateGame();
        Move[] moves =
        [
            new Move { GamePlayerId = 1, TurnNumber = 1, Row = 0, Column = 1 },
            new Move { GamePlayerId = 2, TurnNumber = 2, Row = 0, Column = 0 },
            new Move
            {
                GamePlayerId = 1, TurnNumber = 5, Row = 1, Column = 0, CapturedStones = 1
            }
        ];
        GameReplayService service = new GameReplayService(new GameRulesEngine());

        GameState board = service.Reconstruct(game, moves);

        Assert.AreEqual(StoneColor.Empty, board.Board[0]);
        Assert.AreEqual(1, board.BlackCaptures);
        Assert.AreEqual(6, board.TurnNumber);
        Assert.AreEqual(StoneColor.Black, board.Winner);
        Assert.HasCount(4, board.PositionHistory);
    }

    [TestMethod]
    public void Reconstruct_InconsistentCaptureCount_RejectsCorruptHistory()
    {
        Game game = CreateGame();
        Move[] moves =
        [
            new Move
            {
                GamePlayerId = 1, TurnNumber = 1, Row = 0, Column = 1, CapturedStones = 1
            }
        ];
        GameReplayService service = new GameReplayService(new GameRulesEngine());

        Assert.ThrowsExactly<InvalidOperationException>(() => service.Reconstruct(game, moves));
    }

    private static Game CreateGame()
    {
        Game game = new Game { GameConfiguration = new GameConfiguration { CaptureGoal = 1 } };
        game.GamePlayers.Add(new GamePlayer { GamePlayerId = 1, StoneColor = "Black" });
        game.GamePlayers.Add(new GamePlayer { GamePlayerId = 2, StoneColor = "White" });
        return game;
    }
}
