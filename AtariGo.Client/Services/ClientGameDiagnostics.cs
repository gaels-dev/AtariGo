using System.Diagnostics;

using AtariGo.Diagnostics;

namespace AtariGo.Client.Services;

internal static class ClientGameDiagnostics
{
    internal static void Record(GameOperation operation, DiagnosticEventDetails details)
    {
        DiagnosticEvent entry = GameDiagnostics.Create(operation, details, isClient: true);
        Trace.TraceInformation("{0}", entry);
    }

    internal static void RecordFailure((GameOperation Operation, DiagnosticContext Context) scope,
        Exception failure,
        CancellationToken cancellationToken)
    {
        Record(scope.Operation,
            ClientIdentityDiagnostics.FailureDetails(failure, cancellationToken) with
            {
                Context = scope.Context
            });
    }
}
