using Microsoft.EntityFrameworkCore;

using Grpc.Core;

using AtariGo.Diagnostics;
using AtariGo.Server.Application.Games;

namespace AtariGo.Server.Diagnostics;

internal static class ServerGameDiagnostics
{
    internal static void Record(ILogger logger, GameOperation operation,
        DiagnosticEventDetails details)
    {
        ServerDiagnosticEvents.Record(logger, GameDiagnostics.Create(operation, details));
    }

    internal static void RecordFailure(ILogger logger,
        (GameOperation Operation, DiagnosticContext Context) scope,
        (Exception Error, CancellationToken Cancellation) failure)
    {
        DiagnosticEventDetails details = ServerIdentityDiagnostics.CreateFailureDetails(failure);
        if (failure.Error is RpcException
            {
                StatusCode: StatusCode.InvalidArgument
            or StatusCode.PermissionDenied or StatusCode.Unauthenticated or StatusCode.AlreadyExists
            }
            || scope.Operation == GameOperation.ReconnectExpired)
        {
            details = details with
            {
                Outcome = DiagnosticOutcome.Rejected
            };
        }
        if (failure.Error is DbUpdateConcurrencyException)
        {
            details = details with
            {
                Severity = DiagnosticSeverity.Warning
            };
        }
        Record(logger, scope.Operation, details with
        {
            Context = scope.Context
        });
    }

    internal static DiagnosticContext CreateContext(GameSessionConnection? connection)
    {
        int? userId = connection is not null
            && GameSessionRules.TryGetUserId(connection.PlayerKey, out int registered)
            ? registered : null;
        return new DiagnosticContext
        {
            GameId = connection?.GameId,
            UserId = userId,
            OperationId = connection?.DiagnosticId
        };
    }
}
