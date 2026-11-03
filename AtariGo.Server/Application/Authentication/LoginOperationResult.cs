namespace AtariGo.Server.Application.Authentication;

public sealed record LoginOperationResult(LoginStatus Status)
{
    public int UserId { get; init; }

    public string UserName { get; init; } = string.Empty;

    public string SessionToken { get; init; } = string.Empty;
}
