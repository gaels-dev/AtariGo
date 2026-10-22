namespace AtariGo.Server.Application.Authentication;

public enum LoginStatus
{
    Success,
    UserNotFound,
    IncorrectPassword,
    AccountRestricted
}
