namespace AtariGo.Server.Application.Authentication;

public enum LoginStatus
{
    Success,
    UserNotFound,
    IncorrectPassword,
    AccountRestricted,
    InvalidCredentials,
    ChallengeRequired,
    InvalidCode,
    CodeExpired,
    InvalidChallenge,
    TemporarilyLocked,
    DeliveryFailed,
    RateLimited,
    Unavailable
}
