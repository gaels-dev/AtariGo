namespace AtariGo.Server.Application.PasswordRecovery;

public sealed class PasswordRecoveryOptions
{
    public const int MinimumResendSeconds = 60;

    public string HmacKey { get; set; } = string.Empty;

    public int AccountRequestsPerHour { get; set; } = 5;

    public int IpRequestsPerHour { get; set; } = 20;

    public int ResendSeconds { get; set; } = MinimumResendSeconds;
}
