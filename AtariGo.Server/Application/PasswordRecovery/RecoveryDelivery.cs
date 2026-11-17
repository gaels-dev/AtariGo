using AtariGo.Server.Application.Email;

namespace AtariGo.Server.Application.PasswordRecovery;

public sealed class RecoveryDelivery
{
    public long CodeId { get; init; }

    public EmailMessage Message { get; init; } = new EmailMessage();
}
