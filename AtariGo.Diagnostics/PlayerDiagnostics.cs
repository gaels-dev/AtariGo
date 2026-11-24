namespace AtariGo.Diagnostics;

public static class PlayerDiagnostics
{
    public static DiagnosticEvent Create(PlayerOperation operation, DiagnosticEventDetails details,
        bool isClient = false)
    {
        if (operation == PlayerOperation.Unspecified || !Enum.IsDefined(operation))
        {
            throw new ArgumentOutOfRangeException(nameof(operation));
        }
        var definition = new DiagnosticEventDefinition((int)operation,
            isClient ? "AtariGo.Client.Player" : "AtariGo.Server.Player", operation.ToString());
        return DiagnosticEventFactory.Create(definition, details);
    }

    public static DiagnosticEventDetails CreateResult(Enum status)
    {
        ArgumentNullException.ThrowIfNull(status);
        bool known = Enum.IsDefined(status.GetType(), status);
        DiagnosticOutcome outcome = status.ToString() switch
        {
            "Updated" or "Accepted" => DiagnosticOutcome.Succeeded,
            "Unspecified" => DiagnosticOutcome.Unconfirmed,
            _ => known ? DiagnosticOutcome.Rejected : DiagnosticOutcome.Unconfirmed
        };
        return new DiagnosticEventDetails
        {
            Outcome = outcome,
            Status = known ? new DiagnosticStatus(status) : null,
            Severity = outcome == DiagnosticOutcome.Unconfirmed
                ? DiagnosticSeverity.Warning : DiagnosticSeverity.Information
        };
    }
}
