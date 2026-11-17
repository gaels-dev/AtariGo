namespace AtariGo.Server.Application.PasswordRecovery;

public enum RecoveryResult
{
    Accepted,
    Reset,
    InvalidCode,
    InvalidPassword,
    ConfirmationMismatch,
    RateLimited,
    Unavailable,
    DeliveryFailed,
    NotSaved
}
