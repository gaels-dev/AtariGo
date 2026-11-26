using System.Globalization;
using System.Resources;

using AtariGo.Contracts.Validation;

namespace AtariGo.Server.Application.Email;

public sealed class AuthenticationEmailContent
{
    private readonly ResourceManager _resources;
    private readonly string _prefix;

    private AuthenticationEmailContent(string feature)
    {
        _resources = new ResourceManager("AtariGo.Server.Properties." + feature + "Email",
            typeof(AuthenticationEmailContent).Assembly);
        _prefix = feature + "_Email_";
    }

    public static AuthenticationEmailContent Login
    {
        get;
    } =
        new AuthenticationEmailContent("Login");

    public static AuthenticationEmailContent Recovery
    {
        get;
    } =
        new AuthenticationEmailContent("Recovery");

    public EmailMessage CreateMessage(string recipient, string code, string cultureName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipient);
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(cultureName);
        CultureInfo culture = SelectCulture(cultureName);
        return new EmailMessage
        {
            Recipient = recipient,
            Subject = GetText("Subject", culture),
            Body = string.Format(CultureInfo.InvariantCulture, GetText("Body", culture),
                code, VerificationCodeRules.LifetimeMinutes)
        };
    }

    private string GetText(string part, CultureInfo culture)
    {
        return _resources.GetString(_prefix + part, culture)
            ?? throw new InvalidOperationException("The authentication email resource is missing.");
    }

    private static CultureInfo SelectCulture(string name)
    {
        if (IsLanguage(name, "es"))
        {
            return CultureInfo.GetCultureInfo("es");
        }
        if (IsLanguage(name, "de"))
        {
            return CultureInfo.GetCultureInfo("de");
        }
        return CultureInfo.InvariantCulture;
    }

    private static bool IsLanguage(string name, string language)
    {
        return name.Equals(language, StringComparison.OrdinalIgnoreCase)
            || name.StartsWith(language + "-", StringComparison.OrdinalIgnoreCase);
    }
}
