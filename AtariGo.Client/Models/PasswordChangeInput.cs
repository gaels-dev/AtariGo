namespace AtariGo.Client.Models;

public sealed class PasswordChangeInput
{
    public const int MinimumPasswordLength = 8;
    public const int MaximumPasswordLength = 255;

    public string CurrentPassword { get; set; } = string.Empty;

    public string NewPassword { get; set; } = string.Empty;

    public string Confirmation { get; set; } = string.Empty;

    public PasswordChangeResult? Validate()
    {
        if (string.IsNullOrEmpty(CurrentPassword) || string.IsNullOrEmpty(NewPassword)
            || string.IsNullOrEmpty(Confirmation))
        {
            return PasswordChangeResult.MissingFields;
        }
        if (NewPassword.Length < MinimumPasswordLength
            || NewPassword.Length > MaximumPasswordLength || !NewPassword.Any(char.IsUpper))
        {
            return PasswordChangeResult.InvalidNewPassword;
        }
        if (!string.Equals(NewPassword, Confirmation, StringComparison.Ordinal))
        {
            return PasswordChangeResult.ConfirmationMismatch;
        }
        return null;
    }

    public void Clear()
    {
        CurrentPassword = string.Empty;
        NewPassword = string.Empty;
        Confirmation = string.Empty;
    }
}
