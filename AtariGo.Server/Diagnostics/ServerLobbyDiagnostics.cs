using AtariGo.Diagnostics;

namespace AtariGo.Server.Diagnostics;

internal static class ServerLobbyDiagnostics
{
    internal static void Record(ILogger logger, LobbyOperation operation,
        DiagnosticEventDetails details)
    {
        ServerDiagnosticEvents.Record(logger, LobbyDiagnostics.Create(operation, details));
    }

    internal static void Result(ILogger logger,
        (LobbyOperation Operation, DiagnosticContext Context) scope, Enum status)
    {
        ArgumentNullException.ThrowIfNull(status);
        bool isKnown = Enum.IsDefined(status.GetType(), status);
        string name = status.ToString();
        DiagnosticOutcome outcome = name switch
        {
            "Success" or "EmailAccepted" => DiagnosticOutcome.Succeeded,
            "EmailUnconfirmed" or "EmailUnavailable" => DiagnosticOutcome.Unconfirmed,
            _ => isKnown ? DiagnosticOutcome.Rejected : DiagnosticOutcome.Unconfirmed
        };
        Record(logger, scope.Operation, new DiagnosticEventDetails
        {
            Outcome = outcome,
            Context = scope.Context,
            Status = isKnown ? new DiagnosticStatus(status) : null,
            Severity = outcome == DiagnosticOutcome.Unconfirmed
                ? DiagnosticSeverity.Warning : DiagnosticSeverity.Information
        });
    }
}
