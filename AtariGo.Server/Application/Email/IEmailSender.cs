namespace AtariGo.Server.Application.Email;

public interface IEmailSender
{
    bool IsConfigured { get; }

    Task SendRecoveryCodeAsync(RecoveryEmailMessage message, CancellationToken cancellationToken);
}
