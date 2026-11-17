using System.Globalization;
using System.Resources;

namespace AtariGo.Server.Application.Email;

public static class RecoveryEmailContent
{
    private static readonly ResourceManager resourceManager = new ResourceManager(
        "AtariGo.Server.Properties.RecoveryEmail", typeof(RecoveryEmailContent).Assembly);

    public static string GetSubject(string cultureName)
    {
        return GetText("Recovery_Email_Subject", cultureName);
    }

    public static string GetBody(RecoveryEmailMessage message)
    {
        string template = GetText("Recovery_Email_Body", message.CultureName);
        return string.Format(CultureInfo.InvariantCulture, template, message.Code);
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
