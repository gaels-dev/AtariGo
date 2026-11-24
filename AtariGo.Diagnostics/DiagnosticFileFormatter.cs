using System.Text;
using System.Text.Json;

namespace AtariGo.Diagnostics;

internal static class DiagnosticFileFormatter
{
    private const int DisplayOffsetHours = -6;
    private static readonly TimeSpan displayOffset = TimeSpan.FromHours(DisplayOffsetHours);

    internal static bool CanFormat(DiagnosticEvent entry)
    {
        return entry.TimestampUtc.UtcTicks >= -displayOffset.Ticks;
    }

    internal static byte[] Format(DiagnosticEvent entry)
    {
        DiagnosticContext? context = entry.Details.Context;
        var payload = new
        {
            Timestamp = entry.TimestampUtc.ToOffset(displayOffset),
            entry.Definition.Id,
            entry.Definition.Source,
            entry.Definition.Operation,
            Severity = entry.Details.Severity.ToString(),
            Outcome = entry.Details.Outcome.ToString(),
            context?.GameId,
            context?.RoomId,
            context?.UserId,
            context?.RequestId,
            context?.InvitationId,
            context?.OperationId,
            context?.ActionId,
            TraceId = context?.TraceId?.ToString(),
            entry.Details.Error,
            entry.Details.TraceEventId,
            ApplicationVersion = entry.Details.ApplicationVersion?.ToString()
        };
        return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload) + "\n");
    }

    internal static byte[] FormatSummary(DiagnosticStorageStatus status, DateTimeOffset timestamp)
    {
        var payload = new
        {
            Timestamp = timestamp.ToOffset(displayOffset),
            Source = "AtariGo.Diagnostics",
            Operation = "StorageSummary",
            Severity = DiagnosticSeverity.Warning.ToString(),
            status.Rejected,
            status.WriteFailures
        };
        return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload) + "\n");
    }
}
