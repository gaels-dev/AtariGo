namespace AtariGo.Server.Tests.Matchmaking;

internal sealed class AdvancingMatchmakingTimeProvider : TimeProvider
{
    private long _utcTicks = new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.Zero).Ticks;
    private int _earlyWakeRemaining;
    private int _timerCount;

    public AdvancingMatchmakingTimeProvider(bool wakeEarly = false)
    {
        _earlyWakeRemaining = wakeEarly ? 1 : 0;
    }

    public int TimerCount => _timerCount;

    public override DateTimeOffset GetUtcNow()
    {
        return new DateTimeOffset(Interlocked.Read(ref _utcTicks), TimeSpan.Zero);
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state,
        TimeSpan dueTime, TimeSpan period)
    {
        if (dueTime >= TimeSpan.Zero)
        {
            Interlocked.Increment(ref _timerCount);
            long advancement = dueTime.Ticks;
            if (Interlocked.Exchange(ref _earlyWakeRemaining, 0) != 0)
            {
                advancement = Math.Max(0, advancement - 1);
            }
            Interlocked.Add(ref _utcTicks, advancement);
            dueTime = TimeSpan.Zero;
        }
        return TimeProvider.System.CreateTimer(callback, state, dueTime, period);
    }
}
