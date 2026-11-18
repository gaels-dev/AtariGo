using AtariGo.Server.Application.Email;
using AtariGo.Server.Data.Entities;

namespace AtariGo.Server.Application.Authentication;

public sealed class LoginCodeDelivery
{
    public LoginCodeDelivery(LoginChallenge challenge, EmailMessage message)
    {
        ArgumentNullException.ThrowIfNull(challenge);
        ArgumentNullException.ThrowIfNull(message);
        Challenge = challenge;
        Message = message;
        Binding = challenge.CodeBinding;
    }

    public LoginChallenge Challenge { get; }

    public EmailMessage Message { get; }

    public string Binding { get; }
}
