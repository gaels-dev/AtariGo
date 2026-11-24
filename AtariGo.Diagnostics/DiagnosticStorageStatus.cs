namespace AtariGo.Diagnostics;

public sealed record DiagnosticStorageStatus
{
    public long Accepted { get; init; }

    public long Written { get; init; }

    public long Rejected { get; init; }

    public long Unconfirmed { get; init; }

    public long WriteFailures { get; init; }

    public bool IsDegraded { get; init; }

    public DiagnosticError? LastError { get; init; }

    public long Pending => Accepted - Written - Unconfirmed;
}
