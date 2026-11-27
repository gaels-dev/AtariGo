namespace AtariGo.Server.Application.Authentication;

public enum LoginStatus
{
    Success,
    AccountRestricted,
    InvalidCredentials,
    ChallengeRequired,
    InvalidCode,
    CodeExpired,
    InvalidChallenge,
    TemporarilyLocked,
    DeliveryFailed,
    RateLimited,
    Unavailable,
    AlreadySignedIn
}
