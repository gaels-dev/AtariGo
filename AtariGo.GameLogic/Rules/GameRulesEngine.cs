using AtariGo.GameLogic.Actions;
using AtariGo.GameLogic.Models;

namespace AtariGo.GameLogic.Rules;

public sealed class GameRulesEngine
{
    public MoveResult SubmitMove(GameState state, MoveCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        MoveRejectionReason rejection = ValidateMove(state, command);
        if (rejection != MoveRejectionReason.None)
        {
            return new MoveResult(state, rejection);
        }
        StoneColor[] board = (StoneColor[])state.Board.Clone();
        int index = command.Position.Row * GameState.BoardSize + command.Position.Column;
        board[index] = command.Player;
        int captured = BoardGroups.RemoveCapturedGroups(board, index);
        if (!BoardGroups.HasLiberties(board, BoardGroups.FindGroup(board, index)))
        {
            return new MoveResult(state, MoveRejectionReason.Suicide);
        }
        string position = EncodeBoard(board);
        if (state.PositionHistory.Contains(position, StringComparer.Ordinal))
        {
            return new MoveResult(state, MoveRejectionReason.Repetition);
        }
        GameState next = ApplyCaptures(state, command.Player, captured) with
        {
            Board = board,
            LastMove = command.Position,
            CurrentTurn = GetOpponent(command.Player),
            TurnNumber = state.TurnNumber + 1,
            PositionHistory = [.. state.PositionHistory, position]
        };
        return new MoveResult(next, MoveRejectionReason.None);
    }

    public GameState SkipTurn(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Winner != StoneColor.Empty)
        {
            return state;
        }
        return state with
        {
            CurrentTurn = GetOpponent(state.CurrentTurn),
            TurnNumber = state.TurnNumber + 1
        };
    }

    public static StoneColor GetOpponent(StoneColor player)
    {
        return player == StoneColor.Black ? StoneColor.White : StoneColor.Black;
    }

    private static MoveRejectionReason ValidateMove(GameState state, MoveCommand command)
    {
        if (state.Winner != StoneColor.Empty)
        {
            return MoveRejectionReason.Finished;
        }
        if (command.Player == StoneColor.Empty || command.Player != state.CurrentTurn)
        {
            return MoveRejectionReason.NotYourTurn;
        }
        BoardPosition position = command.Position;
        if (position.Row < 0 || position.Row >= GameState.BoardSize
            || position.Column < 0 || position.Column >= GameState.BoardSize)
        {
            return MoveRejectionReason.OutOfBounds;
        }
        int index = position.Row * GameState.BoardSize + position.Column;
        return state.Board[index] == StoneColor.Empty
            ? MoveRejectionReason.None
            : MoveRejectionReason.Occupied;
    }

    private static GameState ApplyCaptures(GameState state, StoneColor player, int captured)
    {
        int black = state.BlackCaptures;
        int white = state.WhiteCaptures;
        if (player == StoneColor.Black)
        {
            black += captured;
        }
        else
        {
            white += captured;
        }
        StoneColor winner = StoneColor.Empty;
        if (Math.Max(black, white) >= state.CaptureGoal)
        {
            winner = player;
        }
        return state with { BlackCaptures = black, WhiteCaptures = white, Winner = winner };
    }

    private static string EncodeBoard(StoneColor[] board)
    {
        return string.Concat(board.Select(stone => ((int)stone).ToString()));
    }
}
