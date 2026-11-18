namespace AtariGo.Client.Models;

public enum LoginAttemptStatus
{
    Success,
    InvalidCredentials,
    ChallengeRequired,
    AccountRestricted,
    InvalidCode,
    CodeExpired,
    InvalidChallenge,
    TemporarilyLocked,
    DeliveryFailed,
    RateLimited,
    Unavailable,
    Unconfirmed
}
