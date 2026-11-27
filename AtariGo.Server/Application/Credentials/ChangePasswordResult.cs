namespace AtariGo.Server.Application.Credentials;

public enum ChangePasswordResult
{
    Changed,
    IncorrectCurrentPassword,
    InvalidNewPassword,
    ConfirmationMismatch,
    CredentialChanged,
    AccountUnavailable
}
