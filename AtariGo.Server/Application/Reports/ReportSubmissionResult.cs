namespace AtariGo.Server.Application.Reports;

public sealed record ReportSubmissionResult(ReportSubmissionStatus Status, long ReportId = 0);
