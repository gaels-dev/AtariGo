using System.Globalization;

using AtariGo.Client.Models;
using AtariGo.Client.Properties;
using AtariGo.Contracts.Validation;

namespace AtariGo.Client.Services;

public static class AuthenticationMessages
{
    public static string GetMessage(LoginAttemptResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        string message = GetText("LoginVerification_Result_" + result.Status);
        if (result.Status == LoginAttemptStatus.ChallengeRequired)
        {
            return string.Format(CultureInfo.CurrentUICulture, message,
                VerificationCodeRules.LifetimeMinutes);
        }
        if (result.RetryAtUnixSeconds > 0)
        {
            DateTimeOffset retryAt = DateTimeOffset.FromUnixTimeSeconds(result.RetryAtUnixSeconds);
            message += " " + string.Format(CultureInfo.CurrentUICulture,
                GetText("LoginVerification_Msg_RetryAt"), retryAt.ToLocalTime());
        }
        if (!string.IsNullOrWhiteSpace(result.RestrictionReason))
        {
            string key = "LoginVerification_Restriction_" + result.RestrictionReason;
            string? reason = Resources.ResourceManager.GetString(
                key, CultureInfo.CurrentUICulture);
            if (!string.IsNullOrWhiteSpace(reason))
            {
                message += " " + reason;
            }
        }
        return message;
    }

    public static string GetText(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        string message = Resources.ResourceManager.GetString(key, CultureInfo.CurrentUICulture)
            ?? key;
        if (key is "Register_Err_InvalidUsername" or "GuestRegistration_Err_InvalidName")
        {
            return string.Format(CultureInfo.CurrentUICulture, message,
                UserNameRules.MaximumLength);
        }
        return message;
    }

    public static string GetPasswordPolicy()
    {
        return string.Format(CultureInfo.CurrentUICulture,
            GetText("Register_Err_PasswordPolicy"), PasswordPolicy.MinimumPasswordLength,
            PasswordPolicy.MaximumPasswordLength);
    }
}
