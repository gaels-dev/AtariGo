using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

using AtariGo.Diagnostics;

namespace AtariGo.Server.Diagnostics;

internal sealed class SafeLogger : ILogger
{
    private const int UnnamedEventId = 2001;
    private readonly ILogger _inner;
    private readonly string _category;
    private readonly IExternalScopeProvider _scopes;

    internal SafeLogger(ILogger inner, string category, IExternalScopeProvider scopes)
    {
        _inner = inner;
        _category = category;
        _scopes = scopes;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return _scopes.Push(SafeLogContext.Read(state, new DiagnosticContext()));
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel is >= LogLevel.Trace and <= LogLevel.Critical && _inner.IsEnabled(logLevel);
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }
        var details = new DiagnosticEventDetails
        {
            Severity = (DiagnosticSeverity)logLevel,
            Context = ReadContext(state),
            Error = SafeLogErrors.Read(exception, state),
            TraceEventId = eventId.Id,
            Status = SafeLogContext.ReadStatus(state)
        };
        DiagnosticEvent entry = CreateEntry(eventId, state, details);
        _inner.Log(logLevel, new EventId(entry.Definition.Id, entry.Definition.Operation), entry,
            null, Format);
    }

    private DiagnosticEvent CreateEntry<TState>(EventId eventId, TState state,
        DiagnosticEventDetails details)
    {
        if (state is DiagnosticEvent supplied)
        {
            return new DiagnosticEvent(supplied.Definition, supplied.Details with
            {
                Severity = details.Severity,
                Context = SafeLogContext.Read(supplied.Details.Context, details.Context!),
                Error = details.Error ?? supplied.Details.Error
            }, DateTimeOffset.UtcNow);
        }
        string operation = SafeLogOperations.Read(state) ?? "Event_" + Math.Abs((long)eventId.Id)
            .ToString(CultureInfo.InvariantCulture);
        var definition = new DiagnosticEventDefinition(eventId.Id > 0 ? eventId.Id : UnnamedEventId,
            _category, operation);
        return new DiagnosticEvent(definition, details, DateTimeOffset.UtcNow);
    }

    private DiagnosticContext ReadContext<TState>(TState state)
    {
        var context = new StrongBox<DiagnosticContext>(new DiagnosticContext
        {
            TraceId = Activity.Current?.TraceId
        });
        _scopes.ForEachScope(ApplyScope, context);
        return SafeLogContext.Read(state, context.Value!);
    }

    private static void ApplyScope(object? scope, StrongBox<DiagnosticContext> context)
    {
        context.Value = SafeLogContext.Read(scope, context.Value!);
    }

    private static string Format(DiagnosticEvent entry, Exception? exception)
    {
        return Encoding.UTF8.GetString(DiagnosticFileFormatter.Format(entry)).TrimEnd('\n');
    }
}
