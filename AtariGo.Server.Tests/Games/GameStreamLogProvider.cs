using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

namespace AtariGo.Server.Tests.Games;

internal sealed class GameStreamLogProvider : ILoggerProvider
{
    private readonly TaskCompletionSource _disconnectWarning = new TaskCompletionSource(
        TaskCreationOptions.RunContinuationsAsynchronously);

    public Task DisconnectWarning => _disconnectWarning.Task;

    public ConcurrentQueue<(LogLevel Level, string Message, Exception? Exception)> Entries
    {
        get;
    } =
        new();

    public ILogger CreateLogger(string categoryName)
    {
        return new StreamLogger(this);
    }

    public void Dispose()
    {
    }

    private void Record(LogLevel level, string message, Exception? exception)
    {
        Entries.Enqueue((level, message, exception));
        if (level >= LogLevel.Error && exception is not null)
        {
            Console.Error.WriteLine(message);
            Console.Error.WriteLine(exception);
        }
        if (level == LogLevel.Warning
            && message.StartsWith("Game stream disconnected.", StringComparison.Ordinal))
        {
            _disconnectWarning.TrySetResult();
        }
    }

    private sealed class StreamLogger : ILogger
    {
        private readonly GameStreamLogProvider _owner;

        public StreamLogger(GameStreamLogProvider owner)
        {
            _owner = owner;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            _owner.Record(logLevel, formatter(state, exception), exception);
        }
    }
}
