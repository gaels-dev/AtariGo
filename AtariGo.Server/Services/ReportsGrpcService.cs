using System.Data.Common;

using Microsoft.EntityFrameworkCore;

using Grpc.Core;

using AtariGo.Contracts;
using AtariGo.Diagnostics;
using AtariGo.Server.Diagnostics;
using AtariGo.Server.Application.Reports;
using AtariGo.Server.Application.Sessions;
using AtariGo.Server.Data;

namespace AtariGo.Server.Services;

public sealed class ReportsGrpcService : Reports.ReportsBase
{
    private readonly IReportService _reports;
    private readonly ISessionService _sessions;
    private readonly ILogger<ReportsGrpcService> _logger;

    public ReportsGrpcService(IReportService reports, ISessionService sessions,
        ILogger<ReportsGrpcService> logger)
    {
        ArgumentNullException.ThrowIfNull(reports);
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(logger);
        _reports = reports;
        _sessions = sessions;
        _logger = logger;
    }

    private async Task<SubmitReportResponse> SubmitAuthorizedReportAsync(
        SubmitReportRequest request, ServerCallContext context)
    {
        string token = SessionRequestAuthentication.GetSessionToken(context);
        SessionIdentity? identity = await _sessions.ValidateSessionAsync(
            token, context.CancellationToken);
        if (identity is null)
        {
            throw SessionRequestAuthentication.CreateUnauthenticatedException();
        }
        SubmitReportCommand command = new SubmitReportCommand(
            request.GameId, MapCategory(request.Category), request.Description);
        ReportSubmissionResult result = await _reports.SubmitReportAsync(
            identity, command, context.CancellationToken);
        ServerPlayerDiagnostics.Record(_logger, PlayerOperation.SubmitReport,
            PlayerDiagnostics.CreateResult(result.Status) with
            {
                Context = new DiagnosticContext
                {
                    UserId = identity.UserId,
                    GameId = request.GameId > 0 ? request.GameId : null,
                    ReportId = result.ReportId > 0 ? result.ReportId : null
                }
            });
        return new SubmitReportResponse
        {
            Result = MapResult(result.Status),
            ReportId = result.ReportId
        };
    }

    private static string MapCategory(ReportCategory category)
    {
        return category switch
        {
            ReportCategory.Chat => DatabaseValues.ReportCategories.Chat,
            ReportCategory.Inactivity => DatabaseValues.ReportCategories.Inactivity,
            ReportCategory.Manipulation => DatabaseValues.ReportCategories.Manipulation,
            _ => string.Empty
        };
    }

    private static ReportResult MapResult(ReportSubmissionStatus status)
    {
        return status switch
        {
            ReportSubmissionStatus.Accepted => ReportResult.Accepted,
            ReportSubmissionStatus.AlreadyReported => ReportResult.AlreadyReported,
            ReportSubmissionStatus.NotParticipant => ReportResult.NotParticipant,
            ReportSubmissionStatus.OpponentIsGuest => ReportResult.OpponentIsGuest,
            ReportSubmissionStatus.ChatMuted => ReportResult.ChatMuted,
            _ => ReportResult.InvalidRequest
        };
    }

    private static RpcException CreateFailure(StatusCode status)
    {
        return new RpcException(new Status(status, "The report could not be submitted."));
    }

    public override async Task<SubmitReportResponse> SubmitReport(
        SubmitReportRequest request, ServerCallContext context)
    {
        try
        {
            return await SubmitAuthorizedReportAsync(request, context);
        }
        catch (OperationCanceledException exception)
            when (context.CancellationToken.IsCancellationRequested)
        {
            ServerPlayerDiagnostics.RecordFailure(_logger, PlayerOperation.SubmitReport,
                (exception, context.CancellationToken));
            throw new RpcException(new Status(StatusCode.Cancelled,
                "The report request was cancelled."));
        }
        catch (DbException exception)
        {
            ServerPlayerDiagnostics.RecordFailure(_logger, PlayerOperation.SubmitReport,
                (exception, context.CancellationToken));
            throw CreateFailure(StatusCode.Unavailable);
        }
        catch (DbUpdateException exception)
        {
            ServerPlayerDiagnostics.RecordFailure(_logger, PlayerOperation.SubmitReport,
                (exception, context.CancellationToken));
            throw CreateFailure(StatusCode.Aborted);
        }
        catch (InvalidOperationException exception)
        {
            ServerPlayerDiagnostics.RecordFailure(_logger, PlayerOperation.SubmitReport,
                (exception, context.CancellationToken));
            throw CreateFailure(StatusCode.Internal);
        }
        catch (Exception exception)
        {
            ServerPlayerDiagnostics.RecordFailure(_logger, PlayerOperation.SubmitReport,
                (exception, context.CancellationToken));
            throw;
        }
    }
}
