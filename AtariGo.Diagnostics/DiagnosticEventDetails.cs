namespace AtariGo.Diagnostics;

public sealed record DiagnosticEventDetails
{
    public DiagnosticSeverity Severity { get; init; } = DiagnosticSeverity.Information;

    public DiagnosticOutcome Outcome
    {
        get; init;
    }

    public DiagnosticContext? Context
    {
        get; init;
    }

    public DiagnosticError? Error
    {
        get; init;
    }

    public int? TraceEventId
    {
        get; init;
    }

    public Version? ApplicationVersion
    {
        get; init;
    }

    public DiagnosticStatus? Status
    {
        get; init;
    }
}
