namespace AtariGo.Server.Application.PasswordRecovery;

public sealed class PasswordResetCommand
{
    public string Identifier { get; init; } = string.Empty;

    public string Code { get; init; } = string.Empty;

    public string NewPassword { get; init; } = string.Empty;

    public string Confirmation { get; init; } = string.Empty;
}
