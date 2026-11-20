namespace AtariGo.Client.Models;

public enum PasswordChangeResult
{
    Unconfirmed,
    Changed,
    MissingFields,
    IncorrectCurrentPassword,
    InvalidNewPassword,
    ConfirmationMismatch,
    CredentialChanged,
    SessionExpired,
    AccountUnavailable
}
