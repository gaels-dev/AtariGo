using Microsoft.Extensions.Logging.Abstractions;

using AtariGo.Diagnostics;

namespace AtariGo.Server.Diagnostics;

internal sealed class DiagnosticFileLogger : ILogger
{
    private readonly DiagnosticFileLoggerProvider _provider;

    internal DiagnosticFileLogger(DiagnosticFileLoggerProvider provider)
    {
        _provider = provider;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return NullLogger.Instance.BeginScope(state);
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return _provider.IsActive && logLevel != LogLevel.None;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (IsEnabled(logLevel) && state is DiagnosticEvent entry)
        {
            _provider.Write(entry);
        }
    }
}
