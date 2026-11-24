using System.Threading.Channels;

namespace AtariGo.Diagnostics.Tests;

internal sealed class ControlledDiagnosticTimeProvider : TimeProvider
{
    private readonly Channel<ControlledDiagnosticTimer> _timers =
        Channel.CreateUnbounded<ControlledDiagnosticTimer>();

    internal Task<ControlledDiagnosticTimer> NextTimerAsync(CancellationToken cancellationToken)
    {
        return _timers.Reader.ReadAsync(cancellationToken).AsTask();
    }

    internal void FireTimers()
    {
        while (_timers.Reader.TryRead(out ControlledDiagnosticTimer? timer))
        {
            timer.Fire();
        }
    }

    public override DateTimeOffset GetUtcNow()
    {
        return new DateTimeOffset(2026, 11, 23, 0, 0, 0, TimeSpan.Zero);
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime,
        TimeSpan period)
    {
        var timer = new ControlledDiagnosticTimer(callback, state, dueTime);
        _timers.Writer.TryWrite(timer);
        return timer;
    }
}
