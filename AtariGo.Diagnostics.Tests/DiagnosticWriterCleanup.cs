namespace AtariGo.Diagnostics.Tests;

internal sealed class DiagnosticWriterCleanup : IAsyncDisposable
{
    private readonly DiagnosticFileWriter _writer;
    private readonly ControlledDiagnosticTimeProvider _clock;

    internal DiagnosticWriterCleanup(DiagnosticFileWriter writer,
        ControlledDiagnosticTimeProvider clock)
    {
        _writer = writer;
        _clock = clock;
    }

    public async ValueTask DisposeAsync()
    {
        ValueTask closing = _writer.DisposeAsync();
        _clock.FireTimers();
        await closing;
    }
}
