namespace AtariGo.Server.Tests.RoomInvitations;

internal sealed class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset _now = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);

    public void Advance(TimeSpan duration)
    {
        _now = _now.Add(duration);
    }

    public override DateTimeOffset GetUtcNow()
    {
        return _now;
    }
}
