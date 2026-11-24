using System.Diagnostics;

namespace AtariGo.Diagnostics;

public static class IdentityDiagnostics
{
    public static DiagnosticEvent Create(IdentityOperation operation,
        DiagnosticEventDetails details, bool isClient = false)
    {
        ArgumentNullException.ThrowIfNull(details);
        if (operation == IdentityOperation.Unspecified || !Enum.IsDefined(operation))
        {
            throw new ArgumentOutOfRangeException(nameof(operation));
        }
        var definition = new DiagnosticEventDefinition((int)operation,
            isClient ? "AtariGo.Client.Identity" : "AtariGo.Server.Identity", operation.ToString());
        details = details with
        {
            Context = (details.Context ?? new DiagnosticContext()) with
            {
                TraceId = details.Context?.TraceId ?? Activity.Current?.TraceId
            }
        };
        return new DiagnosticEvent(definition, details, DateTimeOffset.UtcNow);
    }

    public static DiagnosticEventDetails Result(IdentityOperation operation, Enum status)
    {
        ArgumentNullException.ThrowIfNull(status);
        if (operation == IdentityOperation.CancelLogin && status.ToString() == "InvalidChallenge")
        {
            return new DiagnosticEventDetails { Outcome = DiagnosticOutcome.Succeeded };
        }
        if (!Enum.IsDefined(status.GetType(), status))
        {
            return new DiagnosticEventDetails
            {
                Outcome = DiagnosticOutcome.Unconfirmed,
                Severity = DiagnosticSeverity.Warning
            };
        }
        DiagnosticOutcome outcome = Outcome(operation, status.ToString());
        return new DiagnosticEventDetails
        {
            Status = new DiagnosticStatus(status),
            Outcome = outcome,
            Severity = outcome == DiagnosticOutcome.Unconfirmed
                || status.ToString() == "RateLimited"
                ? DiagnosticSeverity.Warning : DiagnosticSeverity.Information
        };
    }

    private static DiagnosticOutcome Outcome(IdentityOperation operation, string status)
    {
        return (operation, status) switch
        {
            (IdentityOperation.RespondToFriendRequest, "Rejected") => DiagnosticOutcome.Succeeded,
            (_, "Success" or "Changed" or "Reset" or "Sent" or "Accepted" or "Removed"
                or "Cancelled") => DiagnosticOutcome.Succeeded,
            (_, "ChallengeRequired") => DiagnosticOutcome.Started,
            (_, "Unconfirmed" or "NotSaved" or "DeliveryFailed" or "Unavailable" or "Error")
                => DiagnosticOutcome.Unconfirmed,
            _ => DiagnosticOutcome.Rejected
        };
    }
}
