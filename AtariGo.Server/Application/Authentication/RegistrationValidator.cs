using System.Net.Mail;

namespace AtariGo.Server.Application.Authentication;

public sealed class RegistrationValidator : IRegistrationValidator
{
    public bool IsValid(RegisterAccountCommand command)
    {
        return IsValidUserName(command.UserName)
            && IsValidEmail(command.Email)
            && IsValidPassword(command.Password)
            && PasswordsMatch(command.Password, command.PasswordConfirmation)
            && !string.IsNullOrWhiteSpace(command.ConfirmationText);
    }

    private static bool IsValidUserName(string userName)
    {
        return !string.IsNullOrWhiteSpace(userName)
            && userName.Trim().Length <= 100;
    }

    private static bool IsValidEmail(string email)
    {
        return !string.IsNullOrWhiteSpace(email)
            && email.Trim().Length <= 254
            && MailAddress.TryCreate(email.Trim(), out _);
    }

    private static bool IsValidPassword(string password)
    {
        return !string.IsNullOrWhiteSpace(password)
            && password.Length >= 8
            && password.Length <= 255
            && password.Any(char.IsUpper);
    }

    private static bool PasswordsMatch(string password, string confirmation)
    {
        return string.Equals(password, confirmation, StringComparison.Ordinal);
    }
}
