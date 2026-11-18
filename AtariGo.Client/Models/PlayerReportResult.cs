namespace AtariGo.Client.Models;

public sealed record PlayerReportResult
{
    public PlayerReportStatus Status { get; init; }

    public ClientFailureReason Failure { get; init; }
}
