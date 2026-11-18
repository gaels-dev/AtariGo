using System.Globalization;
using System.Resources;

namespace AtariGo.Server.Application.Email;

public static class LoginEmailContent
{
    private static readonly ResourceManager resourceManager = new ResourceManager(
        "AtariGo.Server.Properties.LoginEmail", typeof(LoginEmailContent).Assembly);

    public static EmailMessage CreateMessage(string recipient, string code, string cultureName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipient);
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(cultureName);
        CultureInfo culture = SelectCulture(cultureName);
        string subject = resourceManager.GetString("Login_Email_Subject", culture)
            ?? throw new InvalidOperationException("The login email subject is missing.");
        string body = resourceManager.GetString("Login_Email_Body", culture)
            ?? throw new InvalidOperationException("The login email body is missing.");
        return new EmailMessage
        {
            Recipient = recipient, Subject = subject,
            Body = string.Format(CultureInfo.InvariantCulture, body, code)
        };
    }

    private static CultureInfo SelectCulture(string name)
    {
        if (name.Equals("es", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("es-", StringComparison.OrdinalIgnoreCase))
        {
            return CultureInfo.GetCultureInfo("es");
        }
        if (name.Equals("de", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("de-", StringComparison.OrdinalIgnoreCase))
        {
            return CultureInfo.GetCultureInfo("de");
        }
        return CultureInfo.InvariantCulture;
    }
}
