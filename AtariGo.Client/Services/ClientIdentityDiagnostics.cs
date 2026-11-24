using System.Diagnostics;

using Grpc.Core;

using AtariGo.Diagnostics;

namespace AtariGo.Client.Services;

internal static class ClientIdentityDiagnostics
{
    internal static void Record(IdentityOperation operation, DiagnosticEventDetails details)
    {
        DiagnosticEvent entry = IdentityDiagnostics.Create(operation, details, isClient: true);
        Trace.TraceInformation("{0}", entry);
    }

    internal static void Completed(IdentityOperation operation, Enum status)
    {
        Record(operation, IdentityDiagnostics.Result(operation, status));
    }

    internal static void Failure(IdentityOperation operation, Exception failure,
        CancellationToken cancellationToken)
    {
        Record(operation, FailureDetails(failure, cancellationToken));
    }

    internal static DiagnosticEventDetails FailureDetails(Exception failure,
        CancellationToken cancellationToken)
    {
        bool cancelled = cancellationToken.IsCancellationRequested
            && (failure is OperationCanceledException aborted
                && aborted.CancellationToken == cancellationToken
                || failure is RpcException { StatusCode: StatusCode.Cancelled });
        return new DiagnosticEventDetails
        {
            Outcome = cancelled ? DiagnosticOutcome.Cancelled : DiagnosticOutcome.Unconfirmed,
            Severity = cancelled ? DiagnosticSeverity.Information : DiagnosticSeverity.Warning,
            Error = DiagnosticError.FromException(failure,
                failure is RpcException rpc ? (int)rpc.StatusCode : null)
        };
    }
}
