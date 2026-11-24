using System.Diagnostics;

namespace AtariGo.Diagnostics;

internal static class DiagnosticEventFactory
{
    internal static DiagnosticEvent Create(DiagnosticEventDefinition definition,
        DiagnosticEventDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        details = details with
        {
            Context = (details.Context ?? new DiagnosticContext()) with
            {
                TraceId = details.Context?.TraceId ?? Activity.Current?.TraceId
            }
        };
        return new DiagnosticEvent(definition, details, DateTimeOffset.UtcNow);
    }
}
