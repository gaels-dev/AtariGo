using AtariGo.Server.Application.Email;

namespace AtariGo.Server.Tests.RoomInvitations;

internal sealed class RecordingRoomInvitationEmailSender : IEmailSender
{
    public bool IsConfigured { get; set; } = true;

    public bool FailDelivery { get; set; }

    public List<EmailMessage> Messages { get; } = new List<EmailMessage>();

    public TaskCompletionSource? Started { get; set; }

    public Task? Completion { get; set; }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        Messages.Add(new EmailMessage
        {
            Recipient = message.Recipient, Subject = message.Subject, Body = message.Body
        });
        Started?.TrySetResult();
        if (Completion is not null)
        {
            await Completion.WaitAsync(cancellationToken);
        }
        if (FailDelivery)
        {
            throw new IOException("Simulated delivery failure.");
        }
    }
}
