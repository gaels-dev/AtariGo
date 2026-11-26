using AtariGo.Server.Application.Reports;
using AtariGo.Server.Application.Sessions;

namespace AtariGo.Server.Tests.Reports;

internal sealed class FailingReportService : IReportService
{
    public Exception Failure { get; set; } = new InvalidOperationException();

    public Task<ReportSubmissionResult> SubmitReportAsync(SessionIdentity identity,
        SubmitReportCommand command, CancellationToken cancellationToken)
    {
        return Task.FromException<ReportSubmissionResult>(Failure);
    }
}
