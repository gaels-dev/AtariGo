using System.Net;
using System.Text;

using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace AtariGo.Server.Tests.Email;

internal sealed class RecordingSmtpClient : SmtpClient
{
    public string Host { get; private set; } = string.Empty;

    public int Port
    {
        get; private set;
    }

    public SecureSocketOptions Security
    {
        get; private set;
    }

    public CancellationToken ConnectToken
    {
        get; private set;
    }

    public string UserName { get; private set; } = string.Empty;

    public CancellationToken AuthenticationToken
    {
        get; private set;
    }

    public string Sender { get; private set; } = string.Empty;

    public string Recipient { get; private set; } = string.Empty;

    public string Subject { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    public CancellationToken SendToken
    {
        get; private set;
    }

    public bool IsDisconnected
    {
        get; private set;
    }

    public bool IsDisposed
    {
        get; private set;
    }

    public bool ShouldFailSend
    {
        get; init;
    }

    public override Task ConnectAsync(string host, int port, SecureSocketOptions options,
        CancellationToken cancellationToken = default)
    {
        Host = host;
        Port = port;
        Security = options;
        ConnectToken = cancellationToken;
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public override Task AuthenticateAsync(Encoding encoding, ICredentials credentials,
        CancellationToken cancellationToken = default)
    {
        NetworkCredential? credential = credentials.GetCredential(new Uri("smtp://example.test"),
            "PLAIN");
        Assert.IsNotNull(credential);
        UserName = credential.UserName;
        AuthenticationToken = cancellationToken;
        return Task.CompletedTask;
    }

    public override Task<SendResult> SendAsync(FormatOptions options, ISendRequest request,
        CancellationToken cancellationToken = default)
    {
        if (ShouldFailSend)
        {
            throw new IOException("Simulated SMTP delivery failure.");
        }
        Sender = request.Message.From.Mailboxes.Single().Address;
        Recipient = request.Message.To.Mailboxes.Single().Address;
        Subject = request.Message.Subject ?? string.Empty;
        Body = request.Message.TextBody ?? string.Empty;
        SendToken = cancellationToken;
        var result = new SmtpSendResult(SmtpStatusCode.Ok, "Accepted",
            request.Message.To.Mailboxes.ToArray(), Array.Empty<MailboxAddress>());
        return Task.FromResult<SendResult>(result);
    }

    public override Task DisconnectAsync(bool quit, CancellationToken cancellationToken = default)
    {
        IsDisconnected = quit;
        return Task.CompletedTask;
    }

    protected override void Dispose(bool disposing)
    {
        IsDisposed = true;
        base.Dispose(disposing);
    }
}
