namespace AtariGo.Server.Application.PasswordRecovery;

public interface IPasswordRecoveryService
{
    Task<RecoveryResult> RequestAsync(RecoveryRequestCommand command,
        CancellationToken cancellationToken);

    Task<RecoveryResult> ResetAsync(PasswordResetCommand command,
        CancellationToken cancellationToken);
}
