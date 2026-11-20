namespace AtariGo.Server.Application.Reports;

public sealed record SubmitReportCommand(long GameId, string Category, string? Description);
