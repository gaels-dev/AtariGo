namespace AtariGo.Server.Application.PasswordRecovery;

public sealed class PasswordRecoveryService : IPasswordRecoveryService
{
    private readonly RecoveryCodeDelivery _delivery;
    private readonly PasswordResetWriter _writer;

    public PasswordRecoveryService(RecoveryCodeDelivery delivery, PasswordResetWriter writer)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentNullException.ThrowIfNull(writer);
        _delivery = delivery;
        _writer = writer;
    }

    public Task<RecoveryResult> RequestAsync(RecoveryRequestCommand command,
        CancellationToken cancellationToken)
    {
        return _delivery.DeliverAsync(command, cancellationToken);
    }

    public Task<RecoveryResult> ResetAsync(PasswordResetCommand command,
        CancellationToken cancellationToken)
    {
        return _writer.ResetAsync(command, cancellationToken);
    }
}
