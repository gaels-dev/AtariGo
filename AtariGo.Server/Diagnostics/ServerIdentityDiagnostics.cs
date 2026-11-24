using System.Text;
using System.Data.Common;

using Microsoft.EntityFrameworkCore;

using Grpc.Core;

using AtariGo.Diagnostics;

namespace AtariGo.Server.Diagnostics;

public static class ServerIdentityDiagnostics
{
    public static void Record(ILogger logger, IdentityOperation operation,
        DiagnosticEventDetails details)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(details);
        LogLevel level = (LogLevel)details.Severity;
        if (logger.IsEnabled(level))
        {
            DiagnosticEvent entry = IdentityDiagnostics.Create(operation, details);
            var id = new EventId((int)operation, operation.ToString());
            logger.Log(level, id, entry, null, Format);
        }
    }

    public static void Completed(ILogger logger, IdentityOperation operation, Enum status)
    {
        Record(logger, operation, IdentityDiagnostics.Result(operation, status));
    }

    public static void Failure(ILogger logger, IdentityOperation operation,
        (Exception Error, CancellationToken Cancellation) failure)
    {
        ArgumentNullException.ThrowIfNull(failure.Error);
        bool cancelled = failure.Cancellation.IsCancellationRequested
            && (failure.Error is OperationCanceledException aborted
                && aborted.CancellationToken == failure.Cancellation
                || failure.Error is RpcException { StatusCode: StatusCode.Cancelled });
        Record(logger, operation, new DiagnosticEventDetails
        {
            Severity = GetSeverity(failure.Error, cancelled),
            Outcome = cancelled ? DiagnosticOutcome.Cancelled : DiagnosticOutcome.Unconfirmed,
            Error = SafeLogErrors.Read(failure.Error, (object?)null)
        });
    }

    private static string Format(DiagnosticEvent entry, Exception? failure)
    {
        return Encoding.UTF8.GetString(DiagnosticFileFormatter.Format(entry)).TrimEnd('\n');
    }

    private static DiagnosticSeverity GetSeverity(Exception failure, bool cancelled)
    {
        if (cancelled)
        {
            return DiagnosticSeverity.Information;
        }
        return failure is DbException or DbUpdateException or InvalidOperationException
            ? DiagnosticSeverity.Error : DiagnosticSeverity.Warning;
    }
}
