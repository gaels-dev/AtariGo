using AtariGo.Contracts.Validation;

namespace AtariGo.Client.Models;

public sealed class PasswordRecoveryInput
{
    public const int MaximumIdentifierLength = EmailAddressRules.MaximumLength;

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
        if (!VerificationCodeRules.IsValid(Code))
        {
            return PasswordRecoveryResult.InvalidCode;
        }
        if (!PasswordPolicy.IsValid(NewPassword))
        {
            return PasswordRecoveryResult.InvalidPassword;
        }
        return string.Equals(NewPassword, Confirmation, StringComparison.Ordinal)
            ? null : PasswordRecoveryResult.ConfirmationMismatch;
    }

    public static bool IsIdentifierValid(string identifier)
    {
        return !string.IsNullOrWhiteSpace(identifier)
            && identifier.Trim().Length <= MaximumIdentifierLength;
    }

    public void Clear()
    {
        Identifier = string.Empty;
        Code = string.Empty;
        NewPassword = string.Empty;
        Confirmation = string.Empty;
    }
}
