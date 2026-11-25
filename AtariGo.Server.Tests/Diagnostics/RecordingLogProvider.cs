using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AtariGo.Server.Tests.Diagnostics;

internal sealed class RecordingLogProvider : ILoggerProvider, ILogger, ISupportExternalScope
{
    private IExternalScopeProvider? _scopes;

    internal ConcurrentQueue<object> States { get; } = new ConcurrentQueue<object>();

    internal ConcurrentQueue<EventId> EventIds { get; } = new ConcurrentQueue<EventId>();

    internal ConcurrentQueue<string> Messages { get; } = new ConcurrentQueue<string>();

    internal ConcurrentQueue<Exception> Exceptions { get; } = new ConcurrentQueue<Exception>();

    internal ConcurrentQueue<object?> Scopes { get; } = new ConcurrentQueue<object?>();

    public ILogger CreateLogger(string categoryName) => this;

    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
    {
        _scopes = scopeProvider;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return NullLogger.Instance.BeginScope(state);
    }

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
    {
        EventIds.Enqueue(eventId);
        if (state is not null)
        {
            States.Enqueue(state);
        }
        if (exception is not null)
        {
            Exceptions.Enqueue(exception);
        }
        Messages.Enqueue(formatter(state, exception));
        _scopes?.ForEachScope((scope, values) => values.Enqueue(scope), Scopes);
    }

    public void Dispose()
    {
        _scopes = null;
    }
}
