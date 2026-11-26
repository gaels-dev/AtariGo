using Microsoft.Extensions.Options;

using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace AtariGo.Server.Application.Email;

public sealed class SmtpEmailSender : IEmailSender
{
    private const int MaximumPort = 65535;
    private const int SmtpTimeoutMilliseconds = 8000;
    private readonly SmtpOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;
    private readonly Func<ISmtpClient> _createClient;

    public SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger)
        : this(options, logger, CreateSmtpClient)
    {
    }

    public SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger,
        Func<ISmtpClient> createClient)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(createClient);
        _options = options.Value;
        _logger = logger;
        _createClient = createClient;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.Host)
        && MailboxAddress.TryParse(_options.Sender, out _)
        && !string.IsNullOrWhiteSpace(_options.UserName)
        && !string.IsNullOrWhiteSpace(_options.Password)
        && _options.Port is > 0 and <= MaximumPort;

    public async Task SendAsync(EmailMessage message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        try
        {
            await SendMessageAsync(message, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException
            && EmailDeliveryService.IsDeliveryFailure(exception))
        {
            _logger.LogWarning("Email delivery failed with {ErrorType}.",
                exception.GetType().Name);
            throw;
        }
    }

    private async Task SendMessageAsync(EmailMessage email,
        CancellationToken cancellationToken)
    {
        using var message = new MimeMessage();
        message.From.Add(new MailboxAddress("Atari Go", _options.Sender));
        message.To.Add(MailboxAddress.Parse(email.Recipient));
        message.Subject = email.Subject;
        message.Body = new TextPart("plain") { Text = email.Body };
        using ISmtpClient client = _createClient();
        SecureSocketOptions security = _options.Port == SmtpOptions.ImplicitTlsPort
            ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
        await client.ConnectAsync(_options.Host, _options.Port, security, cancellationToken);
        await client.AuthenticateAsync(_options.UserName, _options.Password, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }

    private static ISmtpClient CreateSmtpClient()
    {
        return new SmtpClient { Timeout = SmtpTimeoutMilliseconds };
    }
}
