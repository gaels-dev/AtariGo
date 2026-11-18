using AtariGo.Server.Application.Email;

namespace AtariGo.Server.Application.RoomInvitations;

public sealed class RoomInvitationEmailDeliveryService
{
    private readonly EmailDeliveryService _email;
    private readonly RoomInvitationEmailLimiter _limiter;

    public RoomInvitationEmailDeliveryService(EmailDeliveryService email,
        RoomInvitationEmailLimiter limiter)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(limiter);
        _email = email;
        _limiter = limiter;
    }

    public async Task<RoomInvitationResult> SendAsync(RoomInvitationEntry invitation,
        (string Email, string CultureName) recipient, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invitation);
        ArgumentException.ThrowIfNullOrWhiteSpace(recipient.Email);
        ArgumentNullException.ThrowIfNull(recipient.CultureName);
        if (!_email.IsConfigured)
        {
            return new RoomInvitationResult(RoomInvitationOperationStatus.EmailUnavailable);
        }
        cancellationToken.ThrowIfCancellationRequested();
        int retryAfterSeconds = _limiter.TryReserve(invitation);
        if (retryAfterSeconds > 0)
        {
            return new RoomInvitationResult(RoomInvitationOperationStatus.RateLimited)
            {
                RetryAfterSeconds = retryAfterSeconds
            };
        }
        try
        {
            EmailMessage message = RoomInvitationEmailContent.CreateMessage(
                invitation, recipient.Email, recipient.CultureName);
            bool accepted = await _email.TrySendAsync(message, cancellationToken);
            RoomInvitationOperationStatus status = accepted
                ? RoomInvitationOperationStatus.EmailAccepted
                : RoomInvitationOperationStatus.EmailUnconfirmed;
            return new RoomInvitationResult(status, invitation);
        }
        finally
        {
            _limiter.Complete(invitation);
        }
    }
}
