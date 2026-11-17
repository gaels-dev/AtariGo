using System.Text.RegularExpressions;

using AtariGo.Server.Application.Email;

namespace AtariGo.Server.Tests.PasswordRecovery;

internal sealed class RecordingEmailSender : IEmailSender
{
    public bool IsConfigured => true;

    public bool FailDelivery { get; set; }

    public List<string> Codes { get; } = new List<string>();

    public EmailMessage? LastMessage { get; private set; }

    public Task SendAsync(EmailMessage message,
        CancellationToken cancellationToken)
    {
        LastMessage = message;
        if (FailDelivery)
        {
            throw new IOException("Simulated delivery failure.");
        }
        Assert.AreEqual("Owner@example.test", message.Recipient);
        Match code = Regex.Match(message.Body, @"(?<!\d)[0-9]{8}(?!\d)");
        Assert.IsTrue(code.Success);
        Codes.Add(code.Value);
        return Task.CompletedTask;
    }
}
