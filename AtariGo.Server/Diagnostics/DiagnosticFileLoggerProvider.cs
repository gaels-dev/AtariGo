using AtariGo.Diagnostics;

namespace AtariGo.Server.Diagnostics;

[ProviderAlias("DiagnosticFile")]
public sealed class DiagnosticFileLoggerProvider : ILoggerProvider
{
    private readonly DiagnosticFileWriter _writer;
    private int _disposed;

    public DiagnosticFileLoggerProvider(DiagnosticFileWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        _writer = writer;
    }

    internal bool IsActive => Volatile.Read(ref _disposed) == 0;

    public ILogger CreateLogger(string categoryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryName);
        return new DiagnosticFileLogger(this);
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref _disposed, 1);
    }

    internal void Write(DiagnosticEvent entry)
    {
        if (IsActive)
        {
            _writer.TryWrite(entry);
        }
    }
}
