using System.Diagnostics;

using Grpc.Core;

using AtariGo.Client.Models;
using AtariGo.Contracts;
using AtariGo.Diagnostics;

namespace AtariGo.Client.Services;

internal static class ClientLobbyDiagnostics
{
    internal static async Task<T> ExecuteAsync<T>(Func<Task<T>> action,
        LobbyOperation operation, CancellationToken cancellationToken)
    {
        try
        {
            T result = await GrpcCallPolicy.ExecuteAsync(action);
            Record(operation, Result(result));
            return result;
        }
        catch (Exception failure) when (GrpcClientFailures.IsTransportFailure(failure))
        {
            Record(operation, CreateFailureDetails(failure, cancellationToken));
            throw;
        }
    }

    internal static async Task<bool> MoveNextAsync(IAsyncStreamReader<MatchmakingEvent> reader,
        CancellationToken cancellationToken)
    {
        try
        {
            return await GrpcCallPolicy.ExecuteAsync(() => reader.MoveNext(cancellationToken));
        }
        catch (Exception failure) when (GrpcClientFailures.IsTransportFailure(failure))
        {
            Record(LobbyOperation.SearchRequest, CreateFailureDetails(failure, cancellationToken));
            throw;
        }
    }

    internal static void Record(LobbyOperation operation, DiagnosticEventDetails details)
    {
        DiagnosticEvent entry = LobbyDiagnostics.Create(operation, details, isClient: true);
        Trace.TraceInformation("{0}", entry);
    }
    internal static void Failure(LobbyOperation operation, Exception failure,
        CancellationToken cancellationToken)
    {
        Record(operation, CreateFailureDetails(failure, cancellationToken));
    }

    internal static DiagnosticEventDetails CreateFailureDetails(Exception failure,
        CancellationToken cancellationToken)
    {
        Exception cause = failure is ClientRequestException request
            ? request.InnerException ?? failure : failure;
        DiagnosticEventDetails details = ClientIdentityDiagnostics.FailureDetails(
            cause, cancellationToken);
        ClientFailureReason reason = GrpcClientFailures.GetReason(failure);
        if (reason is ClientFailureReason.PermissionDenied or ClientFailureReason.NotFound
            or ClientFailureReason.InvalidRequest or ClientFailureReason.VersionConflict
            or ClientFailureReason.CapacityExceeded or ClientFailureReason.SessionExpired)
        {
            return details with
            {
                Outcome = DiagnosticOutcome.Rejected
            };
        }
        return details;
    }

    private static DiagnosticEventDetails Result<T>(T result)
    {
        if (result is not SendRoomInvitationResponse invitation)
        {
            return new DiagnosticEventDetails { Outcome = DiagnosticOutcome.Succeeded };
        }
        if (!Enum.IsDefined(invitation.DeliveryStatus))
        {
            return new DiagnosticEventDetails
            {
                Outcome = DiagnosticOutcome.Unconfirmed,
                Severity = DiagnosticSeverity.Warning
            };
        }
        DiagnosticOutcome outcome = invitation.DeliveryStatus switch
        {
            RoomInvitationDeliveryStatus.InApp or RoomInvitationDeliveryStatus.EmailAccepted
                => DiagnosticOutcome.Succeeded,
            RoomInvitationDeliveryStatus.EmailUnconfirmed
                or RoomInvitationDeliveryStatus.EmailUnavailable
                or RoomInvitationDeliveryStatus.Unconfirmed => DiagnosticOutcome.Unconfirmed,
            _ => DiagnosticOutcome.Rejected
        };
        return new DiagnosticEventDetails
        {
            Outcome = outcome,
            Status = new DiagnosticStatus(invitation.DeliveryStatus),
            Severity = outcome == DiagnosticOutcome.Unconfirmed
                ? DiagnosticSeverity.Warning : DiagnosticSeverity.Information
        };
    }
}
