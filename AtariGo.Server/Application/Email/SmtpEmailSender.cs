using Microsoft.Extensions.Options;

using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;

using MimeKit;

using AtariGo.Diagnostics;
using AtariGo.Server.Diagnostics;

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
            ServerIdentityDiagnostics.Record(_logger, GetOperation(message.Purpose),
                new DiagnosticEventDetails { Outcome = DiagnosticOutcome.Succeeded });
        }
        catch (Exception exception) when (exception is not OperationCanceledException
            && EmailDeliveryService.IsDeliveryFailure(exception))
        {
            ServerIdentityDiagnostics.Failure(_logger, GetOperation(message.Purpose),
                (exception, cancellationToken));
            throw;
        }
        catch (OperationCanceledException exception)
        {
            ServerIdentityDiagnostics.Failure(_logger, GetOperation(message.Purpose),
                (exception, cancellationToken));
            throw;
        }
    }

    private static IdentityOperation GetOperation(EmailPurpose purpose)
    {
        return purpose switch
        {
            EmailPurpose.LoginVerification => IdentityOperation.SubmitLoginEmail,
            EmailPurpose.PasswordRecovery => IdentityOperation.SubmitRecoveryEmail,
            EmailPurpose.RoomInvitation => IdentityOperation.SubmitInvitationEmail,
            _ => IdentityOperation.SubmitEmail
        };
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
