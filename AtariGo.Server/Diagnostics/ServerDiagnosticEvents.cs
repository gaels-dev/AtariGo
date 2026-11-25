using AtariGo.Diagnostics;

namespace AtariGo.Server.Diagnostics;

internal static class ServerDiagnosticEvents
{
    internal static void Record(ILogger logger, DiagnosticEvent entry)
    {
        ArgumentNullException.ThrowIfNull(logger);
        LogLevel level = (LogLevel)entry.Details.Severity;
        if (logger.IsEnabled(level))
        {
            var id = new EventId(entry.Definition.Id, entry.Definition.Operation);
            logger.Log(level, id, entry, null, Format);
        }
    }

    private static string Format(DiagnosticEvent entry, Exception? failure)
    {
        return DiagnosticConsoleFormatter.Format(entry);
    }
}
