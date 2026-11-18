using System.Globalization;

using AtariGo.Client.Models;
using AtariGo.Client.Properties;

namespace AtariGo.Client.Services;

public static class LoginAttemptMessages
{
    public static string GetMessage(LoginAttemptResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        string message = GetText("LoginVerification_Result_" + result.Status);
        if (result.RetryAtUnixSeconds > 0)
        {
            DateTimeOffset retryAt = DateTimeOffset.FromUnixTimeSeconds(result.RetryAtUnixSeconds);
            message += " " + string.Format(CultureInfo.CurrentUICulture,
                GetText("LoginVerification_Msg_RetryAt"), retryAt.ToLocalTime());
        }
        if (!string.IsNullOrWhiteSpace(result.RestrictionReason))
        {
            message += " " + result.RestrictionReason;
        }
        return message;
    }

    public static string GetText(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return Resources.ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
    }
}
