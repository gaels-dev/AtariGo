using AtariGo.GameLogic.Actions;
using AtariGo.GameLogic.Models;
using AtariGo.GameLogic.Rules;
using AtariGo.Server.Data;
using AtariGo.Server.Data.Entities;

namespace AtariGo.Server.Application.Games;

public sealed class GameActionProcessor
{
    private readonly GameRulesEngine _engine;
    private readonly AtariGoDbContext _dbContext;

    public GameActionProcessor(GameRulesEngine engine, AtariGoDbContext dbContext)
    {
        _engine = engine;
        _dbContext = dbContext;
    }

    public GameActionResult Apply(GameActionContext context)
    {
        string rejection = Validate(context);
        if (rejection.Length == 0)
        {
            rejection = ApplyValidAction(context);
        }
        return new GameActionResult
        {
            ActionId = context.Command.ActionId,
            IsAccepted = rejection.Length == 0,
            Rejection = rejection,
            Version = context.Game.StateVersion + 1
        };
    }

    private static string Validate(GameActionContext context)
    {
        if (context.Runtime.IsTerminal)
        {
            return "Finished";
        }
        if (context.Command.IsSurrender)
        {
            return string.Empty;
        }
        if (context.Runtime.Status != GameRuntimeState.Playing)
        {
            return "Waiting";
        }
        if (context.Now >= context.Runtime.DeadlineUtc)
        {
            return "StaleVersion";
        }
        return context.Command.ExpectedVersion == context.Game.StateVersion
            ? string.Empty : "StaleVersion";
    }

    private string ApplyValidAction(GameActionContext context)
    {
        GameState board = context.Runtime.Board;
        if (context.Command.IsSurrender)
        {
            context.Runtime = context.Runtime with
            {
                Board = board with { Winner = GameRulesEngine.GetOpponent(context.PlayerColor) },
                Status = GameRuntimeState.Completed,
                EndReason = "Surrender"
            };
            return string.Empty;
        }
        MoveCommand move = new MoveCommand(context.PlayerColor,
            new BoardPosition(context.Command.Row, context.Command.Column));
        MoveResult result = _engine.SubmitMove(board, move);
        if (!result.IsAccepted)
        {
            return result.Rejection.ToString();
        }
        AddMove(context, result.State);
        context.Runtime = context.Runtime with
        {
            Board = result.State,
            DeadlineUtc = context.Now.AddSeconds(
                context.Game.GameConfiguration.TurnTimeSeconds)
        };
        if (result.State.Winner != StoneColor.Empty)
        {
            context.Runtime = context.Runtime with
            {
                Status = GameRuntimeState.Completed,
                EndReason = "CaptureGoal"
            };
        }
        return string.Empty;
    }

    private void AddMove(GameActionContext context, GameState next)
    {
        int before = context.Runtime.Board.BlackCaptures + context.Runtime.Board.WhiteCaptures;
        int after = next.BlackCaptures + next.WhiteCaptures;
        _dbContext.Moves.Add(new Move
        {
            GamePlayerId = context.Player.GamePlayerId,
            TurnNumber = context.Runtime.Board.TurnNumber,
            Row = checked((byte)context.Command.Row),
            Column = checked((byte)context.Command.Column),
            PlayedAt = context.Now,
            CapturedStones = after - before
        });
    }
}
