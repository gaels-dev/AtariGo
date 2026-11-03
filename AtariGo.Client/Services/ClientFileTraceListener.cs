using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace AtariGo.Client.Services;

public sealed class ClientFileTraceListener : TextWriterTraceListener
{
    private const int DisplayOffsetHours = -6;

    private static readonly TimeSpan s_displayOffset = TimeSpan.FromHours(DisplayOffsetHours);

    private bool _hasWriteFailure;

    public ClientFileTraceListener(string filePath)
        : base(filePath)
    {
    }

    private void WriteEvent(TraceEventType eventType, string? message)
    {
        if (_hasWriteFailure)
        {
            return;
        }

        try
        {
            DateTimeOffset timestamp = DateTimeOffset.UtcNow.ToOffset(s_displayOffset);
            base.WriteLine($"{timestamp:O} {eventType}: {message}");
            Flush();
        }
        catch (IOException exception)
        {
            _hasWriteFailure = true;
            Debug.WriteLine($"Client log write failed: {exception.GetType().Name}.");
        }
        catch (UnauthorizedAccessException exception)
        {
            _hasWriteFailure = true;
            Debug.WriteLine($"Client log access failed: {exception.GetType().Name}.");
        }
    }

    public override void TraceEvent(
        TraceEventCache? eventCache,
        string source,
        TraceEventType eventType,
        int id,
        string? message)
    {
        WriteEvent(eventType, message);
    }

    public override void TraceEvent(
        TraceEventCache? eventCache,
        string source,
        TraceEventType eventType,
        int id,
        string? format,
        params object?[]? args)
    {
        string? message = args is null
            ? format
            : string.Format(CultureInfo.InvariantCulture, format ?? string.Empty, args);
        WriteEvent(eventType, message);
    }
}
