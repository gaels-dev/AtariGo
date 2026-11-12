using AtariGo.GameLogic.Models;

namespace AtariGo.Server.Application.Games;

public static class GameClock
{
    public static GameRuntimeState Advance(GameRuntimeState runtime, DateTime now, int seconds)
    {
        if (runtime.IsTerminal || now < runtime.DeadlineUtc)
        {
            return runtime;
        }
        if (runtime.Status == GameRuntimeState.Waiting)
        {
            return runtime with
            {
                Status = GameRuntimeState.Cancelled,
                EndReason = "ConnectionTimeout"
            };
        }
        long elapsedTicks = now.Ticks - runtime.DeadlineUtc.Ticks;
        long periodTicks = TimeSpan.FromSeconds(seconds).Ticks;
        long skipped = elapsedTicks / periodTicks + 1;
        StoneColor turn = runtime.Board.CurrentTurn;
        const int PlayersPerCycle = 2;
        if (skipped % PlayersPerCycle != 0)
        {
            turn = turn == StoneColor.Black ? StoneColor.White : StoneColor.Black;
        }
        return runtime with
        {
            Board = runtime.Board with
            {
                CurrentTurn = turn,
                TurnNumber = checked(runtime.Board.TurnNumber + (int)skipped)
            },
            DeadlineUtc = runtime.DeadlineUtc.AddTicks(skipped * periodTicks)
        };
    }
}
