using AtariGo.Server.Application.Email;

namespace AtariGo.Server.Tests.PasswordRecovery;

internal sealed class RecordingEmailSender : IEmailSender
{
    public bool IsConfigured => true;

    public bool FailDelivery { get; set; }

    public List<string> Codes { get; } = new List<string>();

    public Task SendRecoveryCodeAsync(RecoveryEmailMessage message,
        CancellationToken cancellationToken)
    {
        if (FailDelivery)
        {
            throw new IOException("Simulated delivery failure.");
        }
        Assert.AreEqual("Owner@example.test", message.Recipient);
        foreach (string language in new[] { "en", "es-MX", "de-DE" })
        {
            var localized = new RecoveryEmailMessage
            {
                Code = message.Code, CultureName = language
            };
            Assert.IsFalse(string.IsNullOrWhiteSpace(RecoveryEmailContent.GetSubject(language)));
            StringAssert.Contains(RecoveryEmailContent.GetBody(localized), message.Code);
        }
        Codes.Add(message.Code);
        return Task.CompletedTask;
    }
}
