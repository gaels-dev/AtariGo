namespace AtariGo.Server.Application.Authentication;

public enum LoginStatus
{
    Success,
    UserNotFound,
    IncorrectPassword,
    AccountRestricted
}

public sealed record LoginOutcome(
    LoginStatus Status,
    int UserId = 0,
    string UserName = "",
    string SessionToken = "");

public enum RegistrationStatus
{
    Success,
    UsernameTaken,
    EmailTaken,
    Error
}

public sealed record RegisterAccountCommand(
    string UserName,
    string Email,
    string Password,
    string PasswordConfirmation,
    string ConfirmationText);
