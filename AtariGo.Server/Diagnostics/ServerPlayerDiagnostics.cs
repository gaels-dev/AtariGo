using Grpc.Core;

using AtariGo.Diagnostics;

namespace AtariGo.Server.Diagnostics;

internal static class ServerPlayerDiagnostics
{
    internal static void Record(ILogger logger, PlayerOperation operation,
        DiagnosticEventDetails details)
    {
        ServerDiagnosticEvents.Record(logger, PlayerDiagnostics.Create(operation, details));
    }

    internal static void RecordFailure(ILogger logger, PlayerOperation operation,
        (Exception Error, CancellationToken Cancellation) failure)
    {
        DiagnosticEventDetails details = ServerIdentityDiagnostics.CreateFailureDetails(failure);
        if (failure.Error is RpcException
            {
                StatusCode: StatusCode.Unauthenticated
            or StatusCode.PermissionDenied or StatusCode.InvalidArgument
            })
        {
            details = details with
            {
                Outcome = DiagnosticOutcome.Rejected
            };
        }
        Record(logger, operation, details);
    }
}
