using System.Net.Mail;

namespace AtariGo.Server.Application.Authentication;

public sealed class RegistrationValidator
{
    private const int MaximumUserNameLength = 15;
    private const int MaximumEmailLength = 254;
    private const int MinimumPasswordLength = 8;
    private const int MaximumPasswordLength = 255;
    private const string ReservedGuestName = "Guest";
    private const string ReservedGuestPrefix = "Guest-";

    public bool IsValid(RegisterAccountCommand command)
    {
        return IsValidUserName(command.UserName)
            && !IsReservedGuestName(command.UserName)
            && IsValidEmail(command.Email)
            && IsValidPassword(command.Password)
            && PasswordsMatch(command.Password, command.PasswordConfirmation)
            && !string.IsNullOrWhiteSpace(command.ConfirmationText);
    }

    private static bool IsValidUserName(string userName)
    {
        return !string.IsNullOrWhiteSpace(userName)
            && userName.Trim().Length <= MaximumUserNameLength;
    }

    private static bool IsReservedGuestName(string userName)
    {
        string normalizedUserName = userName.Trim();

        return string.Equals(
                normalizedUserName,
                ReservedGuestName,
                StringComparison.OrdinalIgnoreCase)
            || normalizedUserName.StartsWith(
                ReservedGuestPrefix,
                StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsValidEmail(string email)
    {
        return !string.IsNullOrWhiteSpace(email)
            && email.Trim().Length <= MaximumEmailLength
            && MailAddress.TryCreate(email.Trim(), out _);
    }

    private static bool IsValidPassword(string password)
    {
        return !string.IsNullOrWhiteSpace(password)
            && password.Length >= MinimumPasswordLength
            && password.Length <= MaximumPasswordLength
            && password.Any(char.IsUpper);
    }

    private static bool PasswordsMatch(string password, string confirmation)
    {
        return string.Equals(password, confirmation, StringComparison.Ordinal);
    }
}
