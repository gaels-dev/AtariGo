using System.Text.RegularExpressions;

using AtariGo.Server.Application.Email;

namespace AtariGo.Server.Tests.Authentication;

internal sealed class RecordingLoginEmailSender : IEmailSender
{
    public bool IsConfigured { get; set; } = true;

    public bool ShouldFail { get; set; }

    public List<string> Codes { get; } = new List<string>();

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (ShouldFail)
        {
            throw new IOException("Simulated email failure.");
        }
        Match code = Regex.Match(message.Body, @"(?<!\d)[0-9]{8}(?!\d)");
        Assert.IsTrue(code.Success);
        Codes.Add(code.Value);
        return Task.CompletedTask;
    }
}
