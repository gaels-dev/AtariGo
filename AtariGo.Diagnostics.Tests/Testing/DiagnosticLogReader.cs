using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AtariGo.Diagnostics.Tests.Testing;

internal static class DiagnosticLogReader
{
    private static readonly HashSet<string> numericFields =
        new HashSet<string>(StringComparer.Ordinal)
    {
        "Id", "GameId", "RoomId", "UserId", "RequestId", "InvitationId", "ReportId",
        "TraceEventId", "HResult", "NativeCode", "Rejected", "WriteFailures"
    };

    internal static JsonDocument Parse(string line)
    {
        int firstSpace = line.IndexOf(' ');
        int severityEnd = line.IndexOf(": ", firstSpace, StringComparison.Ordinal);
        int fieldsStart = line.LastIndexOf(" [", StringComparison.Ordinal);
        if (firstSpace <= 0 || severityEnd <= firstSpace || fieldsStart <= severityEnd
            || !line.TrimEnd('\n').EndsWith(']'))
        {
            throw new FormatException("Expected a timestamp, severity, message and field suffix.");
        }
        Dictionary<string, object?> fields = CreateFields();
        fields["Timestamp"] = DateTimeOffset.Parse(line[..firstSpace],
            CultureInfo.InvariantCulture).ToString("O", CultureInfo.InvariantCulture);
        fields["Severity"] = line[(firstSpace + 1)..severityEnd];
        fields["Message"] = line[(severityEnd + 2)..fieldsStart];
        string suffix = line[(fieldsStart + 2)..].TrimEnd('\n', ']');
        foreach (string field in suffix.Split("; ", StringSplitOptions.None))
        {
            string[] pair = field.Split('=', 2);
            fields[pair[0]] = numericFields.Contains(pair[0])
                ? long.Parse(pair[1], CultureInfo.InvariantCulture) : pair[1];
        }
        AddNestedFields(fields, line[..fieldsStart]);
        return JsonDocument.Parse(JsonSerializer.Serialize(fields));
    }

    private static Dictionary<string, object?> CreateFields()
    {
        var fields = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["Outcome"] = "Unspecified"
        };
        string[] optionalFields =
        [
            "GameId", "RoomId", "UserId", "RequestId", "InvitationId", "ReportId", "OperationId",
            "ActionId", "TraceId", "TraceEventId", "ApplicationVersion", "Error", "Status",
            "HResult", "NativeCode"
        ];
        foreach (string name in optionalFields)
        {
            fields[name] = null;
        }
        return fields;
    }

    private static void AddNestedFields(Dictionary<string, object?> fields, string message)
    {
        Match error = Regex.Match(message, @"Exception type: ([A-Za-z0-9_.]+)\.",
            RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        if (error.Success)
        {
            fields["Error"] = new
            {
                TypeName = error.Groups[1].Value,
                HResult = fields["HResult"],
                NativeCode = fields["NativeCode"]
            };
        }
        if (fields["Status"] is string status)
        {
            fields["Status"] = new
            {
                TypeName = fields["StatusType"],
                Value = status
            };
        }
    }
}
