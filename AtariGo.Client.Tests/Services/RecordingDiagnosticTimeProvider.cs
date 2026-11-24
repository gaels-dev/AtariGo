namespace AtariGo.Client.Tests.Services;

internal sealed class RecordingDiagnosticTimeProvider : TimeProvider
{
    private readonly TaskCompletionSource _delayScheduled = new TaskCompletionSource(
        TaskCreationOptions.RunContinuationsAsynchronously);

    internal Task DelayScheduled => _delayScheduled.Task;

    public override ITimer CreateTimer(TimerCallback callback, object? state,
        TimeSpan dueTime, TimeSpan period)
    {
        ITimer timer = base.CreateTimer(callback, state, dueTime, period);
        _delayScheduled.TrySetResult();
        return timer;
    }
}
