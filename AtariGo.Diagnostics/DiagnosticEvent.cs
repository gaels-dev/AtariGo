namespace AtariGo.Diagnostics;

public sealed class DiagnosticEvent
{
    public DiagnosticEvent(DiagnosticEventDefinition definition, DiagnosticEventDetails details,
        DateTimeOffset timestamp)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(details);
        ValidateDetails(details);
        Definition = definition;
        Details = details;
        TimestampUtc = timestamp.ToUniversalTime();
    }

    public DiagnosticEventDefinition Definition { get; }

    public DiagnosticEventDetails Details { get; }

    public DateTimeOffset TimestampUtc { get; }

    private static void ValidateDetails(DiagnosticEventDetails details)
    {
        if (!Enum.IsDefined(details.Severity) || !Enum.IsDefined(details.Outcome))
        {
            throw new ArgumentOutOfRangeException(nameof(details),
                "Diagnostic severity and outcome must be defined values.");
        }
        ValidateContext(details.Context);
    }

    private static void ValidateContext(DiagnosticContext? context)
    {
        if (context is null)
        {
            return;
        }
        if (context.GameId is <= 0 || context.RoomId is <= 0 || context.UserId is <= 0
            || context.RequestId is <= 0 || context.InvitationId is <= 0
            || context.ReportId is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(context),
                "Diagnostic participant and resource identifiers must be positive when present.");
        }
    }
}
