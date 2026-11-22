using AtariGo.GameLogic.Models;

namespace AtariGo.Server.Application.Games;

public static class GameTurnConfirmationRules
{
    public static GameRuntimeState Begin(GameRuntimeState state, long version,
        DateTime now, int turnSeconds)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(turnSeconds);
        DateTime deadline = now.AddSeconds(GameRuntimeState.TurnConfirmationSeconds);
        return state with
        {
            PausedTurnRemainingMilliseconds = checked(turnSeconds * 1000L),
            PendingTurnVersion = version,
            TurnConfirmationDeadlineUtc = deadline,
            DeadlineUtc = deadline
        };
    }

    public static GameRuntimeState Expire(GameRuntimeState state, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.TurnConfirmationDeadlineUtc is not DateTime deadline
            || deadline > now || !state.PendingTurnVersion.HasValue)
        {
            return state;
        }
        GameRuntimeState waiting = state with { TurnConfirmationDeadlineUtc = null };
        return GameConnectionRules.Disconnect(waiting, state.Board.CurrentTurn, deadline);
    }

    public static GameRuntimeState Acknowledge(GameRuntimeState state, StoneColor color,
        long version, DateTime now, int turnSeconds)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Status != GameRuntimeState.Playing
            || state.PendingTurnVersion != version || state.Board.CurrentTurn != color)
        {
            return state;
        }
        GameRuntimeState current = Expire(state, now);
        current = GameConnectionRules.Reconnect(current, color, now);
        DateTime? ownDeadline = color == StoneColor.Black
            ? current.BlackReconnectDeadlineUtc : current.WhiteReconnectDeadlineUtc;
        if (ownDeadline.HasValue)
        {
            return current;
        }
        DateTime? otherDeadline = color == StoneColor.Black
            ? current.WhiteReconnectDeadlineUtc : current.BlackReconnectDeadlineUtc;
        return current with
        {
            PendingTurnVersion = null,
            TurnConfirmationDeadlineUtc = null,
            PausedTurnRemainingMilliseconds = otherDeadline.HasValue
                ? current.PausedTurnRemainingMilliseconds : null,
            DeadlineUtc = otherDeadline ?? now.AddSeconds(turnSeconds)
        };
    }
}
