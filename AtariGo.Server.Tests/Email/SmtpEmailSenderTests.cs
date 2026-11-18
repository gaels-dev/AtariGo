using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using MailKit.Security;

using AtariGo.Server.Application.Email;

namespace AtariGo.Server.Tests.Email;

[TestClass]
[TestCategory("Unit")]
public sealed class SmtpEmailSenderTests
{
    [TestMethod]
    [DataRow(465, SecureSocketOptions.SslOnConnect)]
    [DataRow(587, SecureSocketOptions.StartTls)]
    public async Task
        SendAsync_GenericMessage_PreservesContentUsesTlsAndPropagatesCancellationAsync(
        int port, SecureSocketOptions security)
    {
        using var client = new RecordingSmtpClient();
        using var cancellation = new CancellationTokenSource();
        SmtpOptions options = CreateOptions(port);
        var sender = new SmtpEmailSender(Options.Create(options),
            NullLogger<SmtpEmailSender>.Instance, () => client);
        var message = new EmailMessage
        {
            Recipient = "player@example.test",
            Subject = "Custom subject ä",
            Body = "Custom message unrelated to password recovery.\nCódigo: 01234567"
        };
        await sender.SendAsync(message, cancellation.Token);
        Assert.AreEqual(options.Host, client.Host);
        Assert.AreEqual(port, client.Port);
        Assert.AreEqual(security, client.Security);
        Assert.AreEqual(options.UserName, client.UserName);
        Assert.AreEqual(options.Sender, client.Sender);
        Assert.AreEqual(message.Recipient, client.Recipient);
        Assert.AreEqual(message.Subject, client.Subject);
        Assert.AreEqual(message.Body.ReplaceLineEndings("\n"),
            client.Body.ReplaceLineEndings("\n"));
        Assert.AreEqual(cancellation.Token, client.ConnectToken);
        Assert.AreEqual(cancellation.Token, client.AuthenticationToken);
        Assert.AreEqual(cancellation.Token, client.SendToken);
        Assert.IsTrue(client.IsDisconnected);
        Assert.IsTrue(client.IsDisposed);
    }

    [TestMethod]
    public async Task SendAsync_FailedSend_PropagatesFailureAndDisposesClientAsync()
    {
        using var client = new RecordingSmtpClient { ShouldFailSend = true };
        var sender = new SmtpEmailSender(Options.Create(CreateOptions(465)),
            NullLogger<SmtpEmailSender>.Instance, () => client);
        var message = new EmailMessage { Recipient = "player@example.test", Body = "Private code" };
        await Assert.ThrowsAsync<IOException>(() => sender.SendAsync(message,
            CancellationToken.None));
        Assert.IsFalse(client.IsDisconnected);
        Assert.IsTrue(client.IsDisposed);
    }

    [TestMethod]
    public async Task SendAsync_CancelledRequest_PropagatesCancellationAndDisposesClientAsync()
    {
        using var client = new RecordingSmtpClient();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var sender = new SmtpEmailSender(Options.Create(CreateOptions(465)),
            NullLogger<SmtpEmailSender>.Instance, () => client);
        var message = new EmailMessage { Recipient = "player@example.test" };
        await Assert.ThrowsAsync<OperationCanceledException>(() => sender.SendAsync(message,
            cancellation.Token));
        Assert.AreEqual(string.Empty, client.Subject);
        Assert.IsTrue(client.IsDisposed);
    }

    [TestMethod]
    public void IsConfigured_DefaultDependencyInjection_ResolvesConfiguredSenderWithoutFactory()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Options.Create(CreateOptions(465)));
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        using ServiceProvider provider = services.BuildServiceProvider();
        IEmailSender sender = provider.GetRequiredService<IEmailSender>();
        Assert.IsTrue(sender.IsConfigured);
    }

    private static SmtpOptions CreateOptions(int port)
    {
        return new SmtpOptions
        {
            Host = "smtp.example.test",
            Port = port,
            Sender = "sender@example.test",
            UserName = "smtp-user",
            Password = "Test application password"
        };
    }
}
