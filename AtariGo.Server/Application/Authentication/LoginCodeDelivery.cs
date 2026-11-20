using AtariGo.Server.Application.Email;
using AtariGo.Server.Data.Entities;

namespace AtariGo.Server.Application.Authentication;

public sealed class LoginCodeDelivery
{
    public LoginCodeDelivery(PendingLogin pending, AuthenticationCode code, EmailMessage message)
    {
        ArgumentNullException.ThrowIfNull(pending);
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(message);
        Pending = pending;
        Code = code;
        Message = message;
    }

    public PendingLogin Pending
    {
        get;
    }

    public AuthenticationCode Code
    {
        get;
    }

    public EmailMessage Message
    {
        get;
    }
}
