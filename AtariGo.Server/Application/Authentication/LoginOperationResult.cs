namespace AtariGo.Server.Application.Authentication;

public sealed record LoginOperationResult(LoginStatus Status)
{
    public int UserId { get; init; }

    public string UserName { get; init; } = string.Empty;

    public string SessionToken { get; init; } = string.Empty;

    public string ChallengeToken { get; init; } = string.Empty;

    public string MaskedEmail { get; init; } = string.Empty;

    public DateTime? CodeExpiresAt { get; init; }

    public DateTime? RetryAt { get; init; }

    public string RestrictionReason { get; init; } = string.Empty;
}
