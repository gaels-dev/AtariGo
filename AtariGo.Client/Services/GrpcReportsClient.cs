using Grpc.Core;

using AtariGo.Client.Models;
using AtariGo.Contracts;
using AtariGo.Diagnostics;

namespace AtariGo.Client.Services;

public sealed class GrpcReportsClient : IReportClient
{
    private readonly string _sessionToken;
    private readonly Reports.ReportsClient _client;

    public GrpcReportsClient(string sessionToken)
        : this(sessionToken, GrpcClientConnection.Shared.CallInvoker)
    {
    }

    public GrpcReportsClient(string sessionToken, CallInvoker callInvoker)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionToken);
        ArgumentNullException.ThrowIfNull(callInvoker);
        _sessionToken = sessionToken;
        _client = new Reports.ReportsClient(callInvoker);
    }

    public async Task<PlayerReportResult> SubmitReportAsync(
        PlayerReportDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        PlayerReportResult result;
        try
        {
            Metadata headers = new Metadata
            {
                { "authorization", $"Bearer {_sessionToken}" }
            };
            SubmitReportResponse response = await _client.SubmitReportAsync(
                new SubmitReportRequest
                {
                    GameId = draft.GameId,
                    Category = MapCategory(draft.Category),
                    Description = draft.Description ?? string.Empty
                }, GrpcCallPolicy.CreateOptions(cancellationToken, headers)).ResponseAsync;
            cancellationToken.ThrowIfCancellationRequested();
            result = MapResponse(response);
            ClientPlayerDiagnostics.Record(PlayerOperation.SubmitReport,
                CreateDetails(result));
        }
        catch (RpcException exception)
        {
            ClientPlayerDiagnostics.RecordFailure(PlayerOperation.SubmitReport, exception,
                cancellationToken);
            ClientFailureReason reason = cancellationToken.IsCancellationRequested
                ? ClientFailureReason.Cancelled : GrpcClientFailures.GetReason(exception);
            result = CreateFailure(reason);
        }
        catch (OperationCanceledException exception)
        {
            ClientPlayerDiagnostics.RecordFailure(PlayerOperation.SubmitReport, exception,
                cancellationToken);
            result = CreateFailure(cancellationToken.IsCancellationRequested
                ? ClientFailureReason.Cancelled : ClientFailureReason.Timeout);
        }
        return result;
    }

    private static ReportCategory MapCategory(PlayerReportCategory category)
    {
        return category switch
        {
            PlayerReportCategory.Chat => ReportCategory.Chat,
            PlayerReportCategory.Inactivity => ReportCategory.Inactivity,
            PlayerReportCategory.Manipulation => ReportCategory.Manipulation,
            _ => ReportCategory.Unspecified
        };
    }

    private static PlayerReportResult MapResponse(SubmitReportResponse response)
    {
        PlayerReportStatus? status = response.Result switch
        {
            ReportResult.Accepted => PlayerReportStatus.Accepted,
            ReportResult.AlreadyReported => PlayerReportStatus.AlreadyReported,
            ReportResult.NotParticipant => PlayerReportStatus.NotParticipant,
            ReportResult.OpponentIsGuest => PlayerReportStatus.OpponentIsGuest,
            ReportResult.ChatMuted => PlayerReportStatus.ChatMuted,
            ReportResult.InvalidRequest => PlayerReportStatus.InvalidRequest,
            _ => null
        };
        return status.HasValue
            ? new PlayerReportResult { Status = status.Value }
            : CreateFailure(ClientFailureReason.RequestFailed);
    }

    private static DiagnosticEventDetails CreateDetails(PlayerReportResult result)
    {
        if (result.Failure != ClientFailureReason.None)
        {
            return new DiagnosticEventDetails
            {
                Outcome = DiagnosticOutcome.Unconfirmed,
                Severity = DiagnosticSeverity.Warning,
                Status = new DiagnosticStatus(result.Failure)
            };
        }
        return PlayerDiagnostics.CreateResult(result.Status);
    }

    private static PlayerReportResult CreateFailure(ClientFailureReason failure)
    {
        return new PlayerReportResult { Failure = failure };
    }
}
