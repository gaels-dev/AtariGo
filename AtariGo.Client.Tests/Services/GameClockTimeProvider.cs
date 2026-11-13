namespace AtariGo.Client.Tests.Services;

internal sealed class GameClockTimeProvider : TimeProvider
{
    private long _timestamp;

    public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UnixEpoch;

    public void Advance(TimeSpan elapsed)
    {
        _timestamp += elapsed.Ticks;
        UtcNow = UtcNow.Add(elapsed);
    }

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp()
    {
        return _timestamp;
    }

    public override DateTimeOffset GetUtcNow()
    {
        return UtcNow;
    }
}
