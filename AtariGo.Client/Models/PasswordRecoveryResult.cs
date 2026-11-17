namespace AtariGo.Client.Models;

public enum PasswordRecoveryResult
{
    Unconfirmed,
    Accepted,
    Reset,
    MissingFields,
    InvalidCode,
    InvalidPassword,
    ConfirmationMismatch,
    RateLimited,
    Unavailable,
    DeliveryFailed,
    NotSaved
}
