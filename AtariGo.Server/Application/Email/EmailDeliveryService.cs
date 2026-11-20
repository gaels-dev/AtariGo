using MailKit;

namespace AtariGo.Server.Application.Email;

public sealed class EmailDeliveryService
{
    private readonly IEmailSender _sender;

    public EmailDeliveryService(IEmailSender sender)
    {
        ArgumentNullException.ThrowIfNull(sender);
        _sender = sender;
    }

    public bool IsConfigured => _sender.IsConfigured;

    public async Task<bool> TrySendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        try
        {
            await _sender.SendAsync(message, cancellationToken);
        }
        catch (Exception exception) when (IsDeliveryFailure(exception))
        {
            return false;
        }
        finally
        {
            message.Body = string.Empty;
        }
        return true;
    }

    public static bool IsDeliveryFailure(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception is IOException or OperationCanceledException or CommandException
            or ProtocolException or MailKit.Security.AuthenticationException
            or System.Net.Sockets.SocketException
            or System.Security.Authentication.AuthenticationException;
    }
}
