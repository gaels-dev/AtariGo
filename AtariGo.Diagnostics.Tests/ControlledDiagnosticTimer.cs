namespace AtariGo.Diagnostics.Tests;

internal sealed class ControlledDiagnosticTimer : ITimer
{
    private readonly TimerCallback _callback;
    private readonly object? _state;
    private readonly TaskCompletionSource _idle =
        new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _disposed;
    private int _callbacks;

    internal ControlledDiagnosticTimer(TimerCallback callback, object? state, TimeSpan dueTime)
    {
        _callback = callback;
        _state = state;
        DueTime = dueTime;
    }

    internal TimeSpan DueTime { get; }

    public bool Change(TimeSpan dueTime, TimeSpan period)
    {
        return Volatile.Read(ref _disposed) == 0;
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref _disposed, 1);
        if (Volatile.Read(ref _callbacks) == 0)
        {
            _idle.TrySetResult();
        }
    }

    public async ValueTask DisposeAsync()
    {
        Dispose();
        await _idle.Task;
    }

    internal void Fire()
    {
        Interlocked.Increment(ref _callbacks);
        try
        {
            if (Volatile.Read(ref _disposed) == 0)
            {
                _callback(_state);
            }
        }
        finally
        {
            if (Interlocked.Decrement(ref _callbacks) == 0 && Volatile.Read(ref _disposed) != 0)
            {
                _idle.TrySetResult();
            }
        }
    }
}
