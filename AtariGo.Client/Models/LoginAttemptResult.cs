namespace AtariGo.Client.Models;

public sealed class LoginAttemptResult
{
    public LoginAttemptStatus Status { get; init; } = LoginAttemptStatus.Unconfirmed;

    public string UserName { get; init; } = string.Empty;

    public string SessionToken { get; init; } = string.Empty;

    public string ChallengeToken { get; init; } = string.Empty;

    public string MaskedEmail { get; init; } = string.Empty;

    public long CodeExpiresAtUnixSeconds { get; init; }

    public long RetryAtUnixSeconds { get; init; }

    public string RestrictionReason { get; init; } = string.Empty;
}
