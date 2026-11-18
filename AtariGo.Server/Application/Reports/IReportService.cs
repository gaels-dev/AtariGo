using AtariGo.Server.Application.Sessions;

namespace AtariGo.Server.Application.Reports;

public interface IReportService
{
    Task<ReportSubmissionResult> SubmitReportAsync(
        SessionIdentity identity,
        SubmitReportCommand command,
        CancellationToken cancellationToken);
}
