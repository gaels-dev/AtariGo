namespace AtariGo.Server.Application.Email;

public sealed class RecoveryEmailMessage
{
    public string Recipient { get; init; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string CultureName { get; init; } = string.Empty;
}
