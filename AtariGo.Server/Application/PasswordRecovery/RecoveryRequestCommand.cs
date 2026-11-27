namespace AtariGo.Server.Application.PasswordRecovery;

public sealed class RecoveryRequestCommand
{
    public string Identifier { get; init; } = string.Empty;

    public string CultureName { get; init; } = string.Empty;
}
