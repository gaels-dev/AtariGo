using System.Data.Common;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

using Grpc.Core;

using AtariGo.Contracts;
using AtariGo.Contracts.Validation;
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

    public override async Task<SubmitReportResponse> SubmitReport(
        SubmitReportRequest request, ServerCallContext context)
    {
        try
        {
            return await SubmitAuthorizedReportAsync(request, context);
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            throw new RpcException(new Status(StatusCode.Cancelled,
                "The report request was cancelled."));
        }
        catch (DbException exception)
        {
            throw CreateFailure(exception, StatusCode.Unavailable);
        }
        catch (DbUpdateException exception)
        {
            throw CreateFailure(exception, StatusCode.Aborted);
        }
        catch (InvalidOperationException exception)
        {
            throw CreateFailure(exception, StatusCode.Internal);
        }
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
        LogSubmissionResult(request, result);
        return new SubmitReportResponse
        {
            Result = MapResult(result.Status),
            ReportId = result.ReportId
        };
    }

    private void LogSubmissionResult(SubmitReportRequest request, ReportSubmissionResult result)
    {
        _logger.LogInformation("Report submission processed. GameId: {GameId}. "
            + "Result: {Result}. DescriptionLength: {DescriptionLength}. "
            + "MaximumDescriptionLength: {MaximumDescriptionLength}.",
            request.GameId, result.Status, request.Description.Length,
            DescriptionRules.MaximumLength);
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
            ReportSubmissionStatus.DescriptionTooLong => ReportResult.DescriptionTooLong,
            _ => ReportResult.InvalidRequest
        };
    }

    private RpcException CreateFailure(Exception exception, StatusCode status)
    {
        _logger.LogError("Report submission failed. Status: {Status}. "
            + "ErrorType: {ErrorType}. DatabaseError: {DatabaseError}.",
            status, exception.GetType().Name, GetDatabaseErrorCode(exception));
        return new RpcException(new Status(status, "The report could not be submitted."));
    }

    private static int? GetDatabaseErrorCode(Exception exception)
    {
        Exception source = exception.InnerException ?? exception;
        if (source is SqlException sqlException)
        {
            return sqlException.Number;
        }
        if (source is DbException databaseException)
        {
            return databaseException.ErrorCode;
        }
        return null;
    }
}
