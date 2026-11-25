using System.Globalization;
using System.Text;

namespace AtariGo.Diagnostics;

public static class DiagnosticFileFormatter
{
    private const int DisplayOffsetHours = -6;
    private static readonly TimeSpan displayOffset = TimeSpan.FromHours(DisplayOffsetHours);

    public static bool CanFormat(DiagnosticEvent entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return entry.TimestampUtc.UtcTicks >= -displayOffset.Ticks;
    }

    public static byte[] Format(DiagnosticEvent entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        string prefix = FormatPrefix(entry.TimestampUtc, entry.Details.Severity);
        string message = DiagnosticMessageFormatter.Format(entry);
        return Encoding.UTF8.GetBytes(prefix + message + " [" + FormatFields(entry) + "]\n");
    }

    private static string FormatPrefix(DateTimeOffset timestamp, DiagnosticSeverity severity)
    {
        return timestamp.ToOffset(displayOffset).ToString("O", CultureInfo.InvariantCulture)
            + " " + severity + ": ";
    }

    private static string FormatFields(DiagnosticEvent entry)
    {
        var fields = new StringBuilder();
        Append(fields, "Source", entry.Definition.Source);
        Append(fields, "Id", entry.Definition.Id);
        Append(fields, "Operation", entry.Definition.Operation);
        if (entry.Details.Outcome != DiagnosticOutcome.Unspecified)
        {
            Append(fields, "Outcome", entry.Details.Outcome);
        }
        AppendContext(fields, entry.Details.Context);
        Append(fields, "HResult", entry.Details.Error?.HResult);
        Append(fields, "NativeCode", entry.Details.Error?.NativeCode);
        Append(fields, "TraceEventId", entry.Details.TraceEventId);
        Append(fields, "StatusType", entry.Details.Status?.TypeName);
        Append(fields, "Status", entry.Details.Status?.Value);
        Append(fields, "ApplicationVersion", entry.Details.ApplicationVersion);
        Append(fields, "ListeningAddress", entry.Details.Host?.ListeningAddress);
        Append(fields, "EnvironmentName", entry.Details.Host?.EnvironmentName);
        return fields.ToString();
    }

    private static void AppendContext(StringBuilder fields, DiagnosticContext? context)
    {
        Append(fields, "GameId", context?.GameId);
        Append(fields, "RoomId", context?.RoomId);
        Append(fields, "UserId", context?.UserId);
        Append(fields, "RequestId", context?.RequestId);
        Append(fields, "InvitationId", context?.InvitationId);
        Append(fields, "ReportId", context?.ReportId);
        Append(fields, "OperationId", context?.OperationId);
        Append(fields, "ActionId", context?.ActionId);
        Append(fields, "TraceId", context?.TraceId);
    }

    private static void Append(StringBuilder fields, string name, object? value)
    {
        if (value is null)
        {
            return;
        }
        if (fields.Length > 0)
        {
            fields.Append("; ");
        }
        string? text = value is IFormattable formattable
            ? formattable.ToString(null, CultureInfo.InvariantCulture) : value.ToString();
        fields.Append(name).Append('=').Append(text);
    }

    internal static byte[] FormatSummary(DiagnosticStorageStatus status, DateTimeOffset timestamp)
    {
        var fields = new StringBuilder();
        Append(fields, "Source", "AtariGo.Diagnostics");
        Append(fields, "Operation", "StorageSummary");
        Append(fields, "Rejected", status.Rejected);
        Append(fields, "WriteFailures", status.WriteFailures);
        string prefix = FormatPrefix(timestamp, DiagnosticSeverity.Warning);
        return Encoding.UTF8.GetBytes(prefix + "Diagnostic storage summary. [" + fields + "]\n");
    }
}
