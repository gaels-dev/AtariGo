namespace AtariGo.Server.Application.Games;

public static class GameStateTransitions
{
    public static GameRuntimeState Start(GameRuntimeState runtime, DateTime now,
        int turnTimeSeconds)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(turnTimeSeconds);
        if (runtime.Status != GameRuntimeState.Waiting)
        {
            return runtime;
        }
        return runtime with
        {
            Status = GameRuntimeState.Playing,
            DeadlineUtc = now.AddSeconds(turnTimeSeconds)
        };
    }
}
