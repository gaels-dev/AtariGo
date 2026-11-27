using AtariGo.GameLogic.Actions;
using AtariGo.GameLogic.Models;
using AtariGo.GameLogic.Rules;

namespace AtariGo.Server.Application.Games;

public sealed class GameActionProcessor
{
    private readonly GameRulesEngine _engine;

    public GameActionProcessor(GameRulesEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        _engine = engine;
    }

    public GameActionResult Apply(GameActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        string rejection = AdvanceClock(context);
        if (rejection.Length == 0)
        {
            rejection = Validate(context);
        }
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

    private static string AdvanceClock(GameActionContext context)
    {
        GameRuntimeState current = context.Runtime;
        context.Runtime = GameClock.Advance(current, context.Now,
            context.Game.GameConfiguration.TurnTimeSeconds);
        string rejection = string.Empty;
        if (context.Runtime != current && !context.Command.IsSurrender)
        {
            rejection = context.Runtime.IsTerminal ? "Finished" : "StaleVersion";
        }
        return rejection;
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
        if (context.Runtime.PausedTurnRemainingMilliseconds.HasValue)
        {
            return "WaitingReconnect";
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
        string rejection = string.Empty;
        if (context.Command.IsSurrender)
        {
            ApplySurrender(context);
        }
        else
        {
            rejection = ApplyMove(context);
        }
        return rejection;
    }

    private static void ApplySurrender(GameActionContext context)
    {
        GameState board = context.Runtime.Board;
        context.Runtime = context.Runtime with
        {
            Board = board with
            {
                Winner = GameRulesEngine.GetOpponent(context.PlayerColor)
            },
            Status = GameRuntimeState.Completed,
            EndReason = "Surrender"
        };
    }

    private string ApplyMove(GameActionContext context)
    {
        MoveCommand move = new MoveCommand(context.PlayerColor,
            new BoardPosition(context.Command.Row, context.Command.Column));
        MoveResult result = _engine.SubmitMove(context.Runtime.Board, move);
        if (result.IsAccepted)
        {
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
        }
        return result.IsAccepted ? string.Empty : result.Rejection.ToString();
    }
}
