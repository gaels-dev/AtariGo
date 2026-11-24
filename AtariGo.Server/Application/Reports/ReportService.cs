using System.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;

using AtariGo.Diagnostics;
using AtariGo.Server.Diagnostics;
using AtariGo.Server.Application.Sessions;
using AtariGo.Server.Data;
using AtariGo.Server.Data.Entities;

namespace AtariGo.Server.Application.Reports;

public sealed class ReportService : IReportService
{
    private const int MaximumDescriptionLength = 200;
    private const int WindowDays = 30;
    private const int ReportsPerThreshold = 3;
    private const int FirstSanctionDays = 1;
    private const int SecondSanctionDays = 3;
    private const int MaximumSanctionDays = 7;

    private const int SecondSanctionThreshold = ReportsPerThreshold * 2;

    private readonly AtariGoDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;

    public ReportService(AtariGoDbContext dbContext, TimeProvider timeProvider)
        : this(dbContext, timeProvider, NullLogger<ReportService>.Instance)
    {
    }

    public ReportService(AtariGoDbContext dbContext, TimeProvider timeProvider,
        ILogger<ReportService> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task<ReportSubmissionResult> SubmitReportAsync(
        SessionIdentity identity,
        SubmitReportCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(command);
        if (!IsValidCommand(command))
        {
            return new ReportSubmissionResult(ReportSubmissionStatus.InvalidRequest);
        }

        await using IDbContextTransaction transaction = await _dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        (ReportSubmissionResult result, Sanction? sanction) = await StoreReportAsync(
            identity, command, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        RecordCommitted(result, (identity.UserId, command.GameId), sanction);
        return result;
    }

    private async Task<(ReportSubmissionResult Result, Sanction? Sanction)> StoreReportAsync(
        SessionIdentity identity, SubmitReportCommand command,
        CancellationToken cancellationToken)
    {
        List<int?> participants = await _dbContext.GamePlayers.AsNoTracking()
            .Where(player => player.GameId == command.GameId)
            .Select(player => player.UserId)
            .ToListAsync(cancellationToken);
        ReportSubmissionStatus? targetFailure = GetTargetFailure(participants, identity.UserId);
        if (targetFailure.HasValue)
        {
            return (new ReportSubmissionResult(targetFailure.Value), null);
        }

        int opponentId = participants.Single(userId => userId != identity.UserId)!.Value;
        Report? previous = await _dbContext.Reports.AsNoTracking()
            .FirstOrDefaultAsync(report => report.ReporterId == identity.UserId
                && report.GameId == command.GameId, cancellationToken);
        if (previous is not null)
        {
            return (new ReportSubmissionResult(
                ReportSubmissionStatus.AlreadyReported, previous.ReportId), null);
        }

        Report report = CreateReport(identity, opponentId, command);
        if (command.Category == DatabaseValues.ReportCategories.Chat
            && await IsChatMutedAsync(opponentId, report.CreatedAt, cancellationToken))
        {
            return (new ReportSubmissionResult(ReportSubmissionStatus.ChatMuted), null);
        }

        List<int> reporters = await FindRecentReportersAsync(report, cancellationToken);
        _dbContext.Reports.Add(report);
        Sanction? applied = null;
        if (!reporters.Contains(identity.UserId))
        {
            int threshold = reporters.Count + 1;
            if (threshold % ReportsPerThreshold == 0)
            {
                applied = await CreateSanctionAsync(report, threshold, cancellationToken);
                _dbContext.Sanctions.Add(applied);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return (new ReportSubmissionResult(ReportSubmissionStatus.Accepted, report.ReportId),
            applied);
    }

    private void RecordCommitted(ReportSubmissionResult result, (int UserId, long GameId) actor,
        Sanction? sanction)
    {
        if (result.Status != ReportSubmissionStatus.Accepted)
        {
            return;
        }
        var context = new DiagnosticContext
        {
            UserId = actor.UserId,
            GameId = actor.GameId,
            ReportId = result.ReportId
        };
        ServerPlayerDiagnostics.Record(_logger, PlayerOperation.ReportCommitted,
            new DiagnosticEventDetails
            {
                Outcome = DiagnosticOutcome.Succeeded,
                Context = context
            });
        if (sanction is not null)
        {
            PlayerOperation operation =
                sanction.SanctionType == DatabaseValues.SanctionTypes.ChatMute
                ? PlayerOperation.ChatMuteApplied : PlayerOperation.SuspensionApplied;
            ServerPlayerDiagnostics.Record(_logger, operation, new DiagnosticEventDetails
            {
                Outcome = DiagnosticOutcome.Succeeded,
                Context = context with { UserId = sanction.UserId }
            });
        }
    }

    private static bool IsValidCommand(SubmitReportCommand command)
    {
        bool validCategory = command.Category is DatabaseValues.ReportCategories.Chat
            or DatabaseValues.ReportCategories.Inactivity
            or DatabaseValues.ReportCategories.Manipulation;
        return command.GameId > 0 && validCategory
            && (command.Description is null
                || command.Description.Trim().Length <= MaximumDescriptionLength);
    }

    private static ReportSubmissionStatus? GetTargetFailure(
        List<int?> participants, int reporterId)
    {
        if (!participants.Contains(reporterId))
        {
            return ReportSubmissionStatus.NotParticipant;
        }
        if (participants.Count != 2)
        {
            return ReportSubmissionStatus.InvalidRequest;
        }
        return participants.Any(userId => !userId.HasValue)
            ? ReportSubmissionStatus.OpponentIsGuest : null;
    }

    private Task<bool> IsChatMutedAsync(int opponentId, DateTime now,
        CancellationToken cancellationToken)
    {
        return _dbContext.Sanctions.AsNoTracking().AnyAsync(sanction =>
            sanction.UserId == opponentId
            && sanction.SanctionType == DatabaseValues.SanctionTypes.ChatMute
            && sanction.RevokedAt == null && sanction.StartedAt <= now
            && sanction.EndsAt > now, cancellationToken);
    }

    private Task<List<int>> FindRecentReportersAsync(Report current,
        CancellationToken cancellationToken)
    {
        DateTime windowStart = current.CreatedAt.AddDays(-WindowDays);
        return _dbContext.Reports.AsNoTracking()
            .Where(report => report.ReportedUserId == current.ReportedUserId
                && report.Category == current.Category && report.CreatedAt >= windowStart
                && report.CreatedAt <= current.CreatedAt)
            .Select(report => report.ReporterId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    private Report CreateReport(SessionIdentity identity, int opponentId,
        SubmitReportCommand command)
    {
        return new Report
        {
            ReporterId = identity.UserId,
            ReportedUserId = opponentId,
            GameId = command.GameId,
            Category = command.Category,
            Description = string.IsNullOrWhiteSpace(command.Description)
                ? null : command.Description.Trim(),
            CreatedAt = DateTime.UnixEpoch.AddSeconds(
                _timeProvider.GetUtcNow().ToUnixTimeSeconds())
        };
    }

    private async Task<Sanction> CreateSanctionAsync(Report report, int threshold,
        CancellationToken cancellationToken)
    {
        DateTime now = report.CreatedAt;
        string type = report.Category == DatabaseValues.ReportCategories.Chat
            ? DatabaseValues.SanctionTypes.ChatMute
            : DatabaseValues.SanctionTypes.TemporarySuspension;
        DateTime proposedEnd = now.AddDays(GetSanctionDays(threshold));
        DateTime? activeEnd = await _dbContext.Sanctions.AsNoTracking()
            .Where(sanction => sanction.UserId == report.ReportedUserId
                && sanction.SanctionType == type && sanction.RevokedAt == null
                && sanction.StartedAt <= now && sanction.EndsAt > now)
            .MaxAsync(sanction => (DateTime?)sanction.EndsAt, cancellationToken);
        return new Sanction
        {
            UserId = report.ReportedUserId,
            TriggeringReport = report,
            SanctionType = type,
            Threshold = threshold,
            StartedAt = now,
            EndsAt = activeEnd > proposedEnd ? activeEnd.Value : proposedEnd
        };
    }

    private static int GetSanctionDays(int threshold)
    {
        if (threshold == ReportsPerThreshold)
        {
            return FirstSanctionDays;
        }

        if (threshold == SecondSanctionThreshold)
        {
            return SecondSanctionDays;
        }

        return MaximumSanctionDays;
    }
}
