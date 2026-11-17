using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace AtariGo.Server.Application.Email;

public sealed class SmtpEmailSender : IEmailSender
{
    private const int MaximumPort = 65535;
    private const int SmtpTimeoutMilliseconds = 8000;
    private readonly SmtpOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.Host)
        && MailboxAddress.TryParse(_options.Sender, out _)
        && !string.IsNullOrWhiteSpace(_options.UserName)
        && !string.IsNullOrWhiteSpace(_options.Password)
        && _options.Port is > 0 and <= MaximumPort;

    public async Task SendRecoveryCodeAsync(RecoveryEmailMessage message,
        CancellationToken cancellationToken)
    {
        try
        {
            await SendMessageAsync(message, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or CommandException
            or ProtocolException or MailKit.Security.AuthenticationException
            or System.Net.Sockets.SocketException
            or System.Security.Authentication.AuthenticationException)
        {
            _logger.LogWarning("Recovery email delivery failed with {ErrorType}.",
                exception.GetType().Name);
            throw;
        }
    }

    private async Task SendMessageAsync(RecoveryEmailMessage recovery,
        CancellationToken cancellationToken)
    {
        using var message = new MimeMessage();
        message.From.Add(new MailboxAddress("Atari Go", _options.Sender));
        message.To.Add(MailboxAddress.Parse(recovery.Recipient));
        message.Subject = RecoveryEmailContent.GetSubject(recovery.CultureName);
        message.Body = new TextPart("plain") { Text = RecoveryEmailContent.GetBody(recovery) };
        using var client = new SmtpClient { Timeout = SmtpTimeoutMilliseconds };
        SecureSocketOptions security = _options.Port == SmtpOptions.ImplicitTlsPort
            ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
        await client.ConnectAsync(_options.Host, _options.Port, security, cancellationToken);
        await client.AuthenticateAsync(_options.UserName, _options.Password, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
