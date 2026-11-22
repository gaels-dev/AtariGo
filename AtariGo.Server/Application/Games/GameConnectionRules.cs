using AtariGo.GameLogic.Models;

namespace AtariGo.Server.Application.Games;

public static class GameConnectionRules
{
    public static GameRuntimeState Disconnect(GameRuntimeState state, StoneColor color, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Status != GameRuntimeState.Playing || HasDeadline(state, color))
        {
            return state;
        }

        long remaining = state.PausedTurnRemainingMilliseconds
            ?? Math.Max(0, (long)(state.DeadlineUtc - now).TotalMilliseconds);
        DateTime deadline = now.AddMilliseconds(GameRuntimeState.ReconnectionGraceMilliseconds);
        GameRuntimeState updated = color == StoneColor.Black
            ? state with
            {
                PausedTurnRemainingMilliseconds = remaining,
                BlackReconnectDeadlineUtc = deadline
            }
            : state with
            {
                PausedTurnRemainingMilliseconds = remaining,
                WhiteReconnectDeadlineUtc = deadline
            };
        return ScheduleGrace(updated);
    }

    public static GameRuntimeState Reconnect(GameRuntimeState state, StoneColor color, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(state);
        DateTime? deadline = GetDeadline(state, color);
        if (state.Status != GameRuntimeState.Playing || deadline is null || deadline <= now)
        {
            return state;
        }

        GameRuntimeState updated = color == StoneColor.Black
            ? state with
            {
                BlackReconnectDeadlineUtc = null,
                BlackGraceRemainingMilliseconds =
                    GameRuntimeState.ReconnectionGraceMilliseconds
            }
            : state with
            {
                WhiteReconnectDeadlineUtc = null,
                WhiteGraceRemainingMilliseconds =
                    GameRuntimeState.ReconnectionGraceMilliseconds
            };
        if (updated.PendingTurnVersion.HasValue)
        {
            DateTime confirmationDeadline = now.AddSeconds(
                GameRuntimeState.TurnConfirmationSeconds);
            updated = updated with
            {
                TurnConfirmationDeadlineUtc = confirmationDeadline,
                DeadlineUtc = confirmationDeadline
            };
        }
        if (updated.BlackReconnectDeadlineUtc is null
            && updated.WhiteReconnectDeadlineUtc is null
            && updated.PausedTurnRemainingMilliseconds is long remaining
            && !updated.PendingTurnVersion.HasValue)
        {
            updated = updated with
            {
                PausedTurnRemainingMilliseconds = null,
                DeadlineUtc = now.AddMilliseconds(remaining)
            };
        }
        return ScheduleGrace(updated);
    }

    public static GameRuntimeState Resolve(GameRuntimeState state, DateTime now,
        GameConnectionPresence presence)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Status != GameRuntimeState.Playing
            || state.PausedTurnRemainingMilliseconds is null)
        {
            return state;
        }

        GameRuntimeState updated = ScheduleGrace(ExpireDeadlines(state, now));
        if (updated.BlackGraceRemainingMilliseconds == 0 && presence.WhiteConnected)
        {
            return Finish(updated, StoneColor.White);
        }
        if (updated.WhiteGraceRemainingMilliseconds == 0 && presence.BlackConnected)
        {
            return Finish(updated, StoneColor.Black);
        }
        if (updated.BlackGraceRemainingMilliseconds == 0
            && updated.WhiteGraceRemainingMilliseconds == 0)
        {
            return updated with
            {
                Status = GameRuntimeState.Cancelled,
                EndReason = "BothDisconnected"
            };
        }
        return updated;
    }

    public static bool HasExpiredGrace(GameRuntimeState state, StoneColor color, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.Status == GameRuntimeState.Playing
            && GetDeadline(state, color) is DateTime deadline && deadline <= now;
    }

    private static GameRuntimeState ExpireDeadlines(GameRuntimeState state, DateTime now)
    {
        GameRuntimeState updated = state;
        if (state.BlackReconnectDeadlineUtc <= now)
        {
            updated = updated with
            {
                BlackReconnectDeadlineUtc = null,
                BlackGraceRemainingMilliseconds = 0
            };
        }
        if (state.WhiteReconnectDeadlineUtc <= now)
        {
            updated = updated with
            {
                WhiteReconnectDeadlineUtc = null,
                WhiteGraceRemainingMilliseconds = 0
            };
        }
        return updated;
    }

    private static GameRuntimeState Finish(GameRuntimeState state, StoneColor winner)
    {
        return state with
        {
            Board = state.Board with { Winner = winner },
            Status = GameRuntimeState.Completed,
            EndReason = "DisconnectionTimeout"
        };
    }

    private static GameRuntimeState ScheduleGrace(GameRuntimeState state)
    {
        DateTime? next = state.TurnConfirmationDeadlineUtc;
        if (state.BlackReconnectDeadlineUtc is DateTime black
            && (next is null || black < next))
        {
            next = black;
        }
        if (state.WhiteReconnectDeadlineUtc is DateTime white
            && (next is null || white < next))
        {
            next = white;
        }
        if (next is null)
        {
            return state;
        }
        return state with { DeadlineUtc = next.Value };
    }

    private static bool HasDeadline(GameRuntimeState state, StoneColor color)
    {
        return GetDeadline(state, color).HasValue;
    }

    private static DateTime? GetDeadline(GameRuntimeState state, StoneColor color)
    {
        return color == StoneColor.Black
            ? state.BlackReconnectDeadlineUtc : state.WhiteReconnectDeadlineUtc;
    }

    private static long GetBudget(GameRuntimeState state, StoneColor color)
    {
        return color == StoneColor.Black
            ? state.BlackGraceRemainingMilliseconds : state.WhiteGraceRemainingMilliseconds;
    }
}
