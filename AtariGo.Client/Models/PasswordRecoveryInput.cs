namespace AtariGo.Client.Models;

public sealed class PasswordRecoveryInput
{
    public const int MaximumIdentifierLength = 254;
    public const int CodeLength = 8;

    public string Identifier { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string NewPassword { get; set; } = string.Empty;

    public string Confirmation { get; set; } = string.Empty;

    public PasswordRecoveryResult? Validate()
    {
        if (!IsIdentifierValid(Identifier) || string.IsNullOrEmpty(NewPassword)
            || string.IsNullOrEmpty(Confirmation))
        {
            return PasswordRecoveryResult.MissingFields;
        }
        if (Code.Length != CodeLength || Code.Any(character => character is < '0' or > '9'))
        {
            return PasswordRecoveryResult.InvalidCode;
        }
        if (NewPassword.Length < PasswordChangeInput.MinimumPasswordLength
            || NewPassword.Length > PasswordChangeInput.MaximumPasswordLength
            || !NewPassword.Any(char.IsUpper))
        {
            return PasswordRecoveryResult.InvalidPassword;
        }
        return string.Equals(NewPassword, Confirmation, StringComparison.Ordinal)
            ? null : PasswordRecoveryResult.ConfirmationMismatch;
    }

    public static bool IsIdentifierValid(string identifier)
    {
        return !string.IsNullOrWhiteSpace(identifier)
            && identifier.Length <= MaximumIdentifierLength;
    }

    public void Clear()
    {
        Identifier = string.Empty;
        Code = string.Empty;
        NewPassword = string.Empty;
        Confirmation = string.Empty;
    }
}
