using AtariGo.Contracts.Validation;

namespace AtariGo.Server.Application.Authentication;

public sealed class LoginVerificationOptions
{
    public const int MaximumFailedAttempts = 3;
    public const int MaximumDeliveryFailures = 2;
    public const int MinimumResendSeconds = 60;
    public const string Purpose = "TwoFactor";

    public static TimeSpan CodeLifetime { get; } = VerificationCodeRules.Lifetime;

    public static TimeSpan LockDuration { get; } = TimeSpan.FromMinutes(15);

    public static TimeSpan IssuanceLifetime { get; } = TimeSpan.FromMinutes(30);

    public int ResendSeconds { get; set; } = MinimumResendSeconds;

    public int AccountRequestsPerHour { get; set; } = 5;

    public int IpRequestsPerHour { get; set; } = 120;

    public int MaximumPendingLogins { get; set; } = 10000;
}
