using AtariGo.GameLogic.Actions;
using AtariGo.GameLogic.Models;
using AtariGo.GameLogic.Rules;
using AtariGo.Server.Data;
using AtariGo.Server.Data.Entities;

namespace AtariGo.Server.Application.Games;

public sealed class GameReplayService
{
    private readonly GameRulesEngine _engine;

    public GameReplayService(GameRulesEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        _engine = engine;
    }

    public GameState Reconstruct(Game game, IReadOnlyList<Move> moves)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(moves);
        GameState board = new GameState { CaptureGoal = game.GameConfiguration.CaptureGoal };
        foreach (Move move in moves)
        {
            board = ApplyMove(game, board, move);
        }
        foreach (GamePlayer player in game.GamePlayers)
        {
            if (player.Result == DatabaseValues.GamePlayerResults.Won)
            {
                board = board with { Winner = GetColor(player) };
            }
        }
        return board;
    }

    private GameState ApplyMove(Game game, GameState board, Move move)
    {
        if (move.TurnNumber < board.TurnNumber)
        {
            throw new InvalidOperationException("The recorded turn sequence is invalid.");
        }
        if ((move.TurnNumber - board.TurnNumber) % 2 != 0)
        {
            board = board with { CurrentTurn = GameRulesEngine.GetOpponent(board.CurrentTurn) };
        }
        board = board with { TurnNumber = move.TurnNumber };
        GamePlayer player = FindPlayer(game, move.GamePlayerId);
        MoveCommand command = new MoveCommand(GetColor(player),
            new BoardPosition(move.Row, move.Column));
        MoveResult result = _engine.SubmitMove(board, command);
        int captured = result.State.BlackCaptures + result.State.WhiteCaptures
            - board.BlackCaptures - board.WhiteCaptures;
        if (!result.IsAccepted || captured != move.CapturedStones)
        {
            throw new InvalidOperationException("The recorded move cannot be reconstructed.");
        }
        return result.State;
    }

    private static GamePlayer FindPlayer(Game game, long playerId)
    {
        foreach (GamePlayer player in game.GamePlayers)
        {
            if (player.GamePlayerId == playerId)
            {
                return player;
            }
        }
        throw new InvalidOperationException("The recorded move participant is missing.");
    }

    private static StoneColor GetColor(GamePlayer player)
    {
        if (player.StoneColor == GameSessionRules.BlackStoneColor)
        {
            return StoneColor.Black;
        }
        return StoneColor.White;
    }
}
