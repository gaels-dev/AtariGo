using System.Text;

namespace AtariGo.Diagnostics;

public static class DiagnosticConsoleFormatter
{
    public static string Format(DiagnosticEvent entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return entry.Definition.Source.StartsWith("Microsoft.Hosting.", StringComparison.Ordinal)
            ? DiagnosticMessageFormatter.Format(entry)
            : Encoding.UTF8.GetString(DiagnosticFileFormatter.Format(entry))
                .TrimEnd('\n');
    }
}
