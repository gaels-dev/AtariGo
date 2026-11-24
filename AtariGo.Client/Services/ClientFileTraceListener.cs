using System.Diagnostics;

using AtariGo.Diagnostics;

namespace AtariGo.Client.Services;

public sealed class ClientFileTraceListener : TraceListener
{
    private readonly DiagnosticFileWriter _writer;

    public ClientFileTraceListener(DiagnosticFileWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        _writer = writer;
    }

    public DiagnosticStorageStatus Status => _writer.Status;

    public override bool IsThreadSafe => true;

    public override void Write(string? message)
    {
        TraceEvent(null, "AtariGo.Client", TraceEventType.Information, 0, message);
    }

    public override void WriteLine(string? message)
    {
        Write(message);
    }

    public override void TraceEvent(TraceEventCache? eventCache, string source,
        TraceEventType eventType, int id, string? message)
    {
        TraceEvent(eventCache, source, eventType, id, message, null);
    }

    public override void TraceEvent(TraceEventCache? eventCache, string source,
        TraceEventType eventType, int id, string? format, params object?[]? args)
    {
        if (Filter is not null
            && !Filter.ShouldTrace(eventCache, source, eventType, id, format, args, null, null))
        {
            return;
        }
        var trace = new ClientTraceEvent(source, eventType, id);
        _writer.TryWrite(trace.Create(format, args));
    }

    public override void TraceData(TraceEventCache? eventCache, string source,
        TraceEventType eventType, int id, params object?[]? data)
    {
        if (Filter is not null
            && !Filter.ShouldTrace(eventCache, source, eventType, id, null, null, null, data))
        {
            return;
        }
        if (data is not null)
        {
            foreach (DiagnosticEvent entry in data.OfType<DiagnosticEvent>())
            {
                _writer.TryWrite(entry);
            }
        }
    }

    public override void TraceData(TraceEventCache? eventCache, string source,
        TraceEventType eventType, int id, object? data)
    {
        if (Filter is not null
            && !Filter.ShouldTrace(eventCache, source, eventType, id, null, null, data, null))
        {
            return;
        }
        if (data is DiagnosticEvent entry)
        {
            _writer.TryWrite(entry);
        }
    }
}
