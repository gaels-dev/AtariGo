using System.Globalization;
using System.Resources;

namespace AtariGo.Server.Application.Email;

public static class RecoveryEmailContent
{
    private static readonly ResourceManager resourceManager = new ResourceManager(
        "AtariGo.Server.Properties.RecoveryEmail", typeof(RecoveryEmailContent).Assembly);

    public static EmailMessage CreateMessage(string recipient, string code, string cultureName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipient);
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(cultureName);
        return new EmailMessage
        {
            Recipient = recipient,
            Subject = GetSubject(cultureName),
            Body = GetBody(code, cultureName)
        };
    }

    public static string GetSubject(string cultureName)
    {
        ArgumentNullException.ThrowIfNull(cultureName);
        return GetText("Recovery_Email_Subject", cultureName);
    }

    public static string GetBody(string code, string cultureName)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(cultureName);
        string template = GetText("Recovery_Email_Body", cultureName);
        return string.Format(CultureInfo.InvariantCulture, template, code);
    }

    private static string GetText(string key, string cultureName)
    {
        CultureInfo culture = SelectCulture(cultureName);
        return resourceManager.GetString(key, culture)
            ?? throw new InvalidOperationException("The recovery email resource is missing.");
    }

    private static CultureInfo SelectCulture(string cultureName)
    {
        if (IsLanguage(cultureName, "es"))
        {
            return CultureInfo.GetCultureInfo("es");
        }
        if (IsLanguage(cultureName, "de"))
        {
            return CultureInfo.GetCultureInfo("de");
        }
        return CultureInfo.InvariantCulture;
    }

    private static bool IsLanguage(string cultureName, string language)
    {
        return string.Equals(cultureName, language, StringComparison.OrdinalIgnoreCase)
            || cultureName.StartsWith(language + "-", StringComparison.OrdinalIgnoreCase);
    }
}
