using System.Net.Mail;

namespace AtariGo.Server.Application.Authentication;

public sealed class RegistrationValidator
{
    private const int MaximumEmailLength = 254;

    public bool IsValid(RegisterAccountCommand command)
    {
        return UserNameRules.IsValid(command.UserName)
            && IsValidEmail(command.Email)
            && PasswordPolicy.IsValid(command.Password)
            && PasswordsMatch(command.Password, command.PasswordConfirmation)
            && !string.IsNullOrWhiteSpace(command.ConfirmationText);
    }

    private static bool IsValidEmail(string email)
    {
        return !string.IsNullOrWhiteSpace(email)
            && email.Trim().Length <= MaximumEmailLength
            && MailAddress.TryCreate(email.Trim(), out _);
    }

    private static bool PasswordsMatch(string password, string confirmation)
    {
        return string.Equals(password, confirmation, StringComparison.Ordinal);
    }
}
