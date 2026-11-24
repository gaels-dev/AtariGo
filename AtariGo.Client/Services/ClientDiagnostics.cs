using System.Diagnostics;
using System.IO;

using AtariGo.Diagnostics;

namespace AtariGo.Client.Services;

public sealed class ClientDiagnostics : IAsyncDisposable
{
    private const int LifecycleEventId = 1000;
    private readonly DiagnosticFileWriter _writer;
    private readonly ClientFileTraceListener _listener;
    private readonly TaskCompletionSource _closed = new TaskCompletionSource(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private int _isStopping;

    public ClientDiagnostics(DiagnosticFileOptions options, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        _writer = new DiagnosticFileWriter(options, timeProvider);
        _listener = new ClientFileTraceListener(_writer);
        Trace.Listeners.Add(_listener);
        Record("ClientStarted", DiagnosticOutcome.Started);
    }

    public DiagnosticStorageStatus Status => _writer.Status;

    public static ClientDiagnostics Start()
    {
        string directory = Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData), "AtariGo", "logs");
        return new ClientDiagnostics(new DiagnosticFileOptions(directory, "client"));
    }

    public Task FlushAsync(CancellationToken cancellationToken = default)
    {
        return _writer.FlushAsync(cancellationToken);
    }

    public void RecordFailure(string operation, Exception failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        var definition = new DiagnosticEventDefinition(LifecycleEventId, "AtariGo.Client",
            operation);
        var details = new DiagnosticEventDetails
        {
            Severity = DiagnosticSeverity.Error,
            Outcome = DiagnosticOutcome.Failed,
            Error = DiagnosticError.FromException(failure)
        };
        _writer.TryWrite(new DiagnosticEvent(definition, details, DateTimeOffset.UtcNow));
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _isStopping, 1) == 0)
        {
            try
            {
                Trace.Listeners.Remove(_listener);
                _listener.Dispose();
                Record("ClientStopped", DiagnosticOutcome.Stopped);
                await _writer.DisposeAsync().ConfigureAwait(false);
                _closed.TrySetResult();
            }
            catch (Exception failure)
            {
                _closed.TrySetException(failure);
            }
        }
        await _closed.Task.ConfigureAwait(false);
    }

    private void Record(string operation, DiagnosticOutcome outcome)
    {
        var definition = new DiagnosticEventDefinition(LifecycleEventId, "AtariGo.Client",
            operation);
        var details = new DiagnosticEventDetails
        {
            Outcome = outcome,
            ApplicationVersion = typeof(ClientDiagnostics).Assembly.GetName().Version
        };
        _writer.TryWrite(new DiagnosticEvent(definition, details, DateTimeOffset.UtcNow));
    }
}
