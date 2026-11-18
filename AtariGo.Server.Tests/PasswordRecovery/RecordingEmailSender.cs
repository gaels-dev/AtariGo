using System.Text.RegularExpressions;

using AtariGo.Server.Application.Email;

namespace AtariGo.Server.Tests.PasswordRecovery;

internal sealed class RecordingEmailSender : IEmailSender
{
    public bool IsConfigured { get; set; } = true;

    public string ExpectedRecipient { get; init; } = "Owner@example.test";

    public bool FailDelivery { get; set; }

    public List<string> Codes { get; } = new List<string>();

    public EmailMessage? LastMessage { get; private set; }

    public string LastAttemptedCode { get; private set; } = string.Empty;

    public TaskCompletionSource? SendStarted { get; set; }

    public Task? SendCompletion { get; set; }

    public async Task SendAsync(EmailMessage message,
        CancellationToken cancellationToken)
    {
        LastMessage = message;
        Assert.AreEqual(ExpectedRecipient, message.Recipient);
        Match code = Regex.Match(message.Body, @"(?<!\d)[0-9]{8}(?!\d)");
        Assert.IsTrue(code.Success);
        LastAttemptedCode = code.Value;
        bool failDelivery = FailDelivery;
        Task? completion = SendCompletion;
        SendStarted?.TrySetResult();
        if (completion is not null)
        {
            await completion.WaitAsync(cancellationToken);
        }
        if (failDelivery)
        {
            throw new IOException("Simulated delivery failure.");
        }
        Codes.Add(code.Value);
    }
}
