using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using AtariGo.Diagnostics;
using AtariGo.Server.Application.Email;
using AtariGo.Server.Tests.Email;

namespace AtariGo.Server.Tests.Diagnostics;

[TestClass]
[TestCategory("Unit")]
public sealed class IdentityEmailLoggingTests
{
    private readonly TestContext _testContext;

    public IdentityEmailLoggingTests(TestContext testContext)
    {
        ArgumentNullException.ThrowIfNull(testContext);
        _testContext = testContext;
    }

    [TestMethod]
    [DataRow(false, IdentityOperation.SubmitLoginEmail, DiagnosticOutcome.Succeeded)]
    [DataRow(true, IdentityOperation.SubmitLoginEmail, DiagnosticOutcome.Unconfirmed)]
    public async Task SendAsync_LoginPurpose_RecordsSubmissionWithoutMessageAsync(bool fail,
        IdentityOperation expected, DiagnosticOutcome outcome)
    {
        using var records = new RecordingLogProvider();
        using var factory = new LoggerFactory(new[] { records });
        using var client = new RecordingSmtpClient { ShouldFailSend = fail };
        var options = new SmtpOptions
        {
            Host = "smtp.invalid",
            Port = 465,
            Sender = "private@example.invalid",
            UserName = "private",
            Password = "private"
        };
        var sender = new SmtpEmailSender(Options.Create(options),
            factory.CreateLogger<SmtpEmailSender>(), () => client);
        EmailMessage message = AuthenticationEmailContent.Login.CreateMessage(
            "private@example.invalid", "private", "en");
        if (fail)
        {
            await Assert.ThrowsAsync<IOException>(() => sender.SendAsync(message,
                _testContext.CancellationToken));
        }
        else
        {
            await sender.SendAsync(message, _testContext.CancellationToken);
        }
        DiagnosticEvent entry = (DiagnosticEvent)records.States.Single();

        Assert.AreEqual((int)expected, entry.Definition.Id);
        Assert.AreEqual(outcome, entry.Details.Outcome);
        Assert.DoesNotContain("private", records.Messages.Single(), StringComparison.Ordinal);
    }
}
