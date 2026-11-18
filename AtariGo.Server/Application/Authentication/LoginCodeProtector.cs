using Microsoft.Extensions.Options;

using AtariGo.Server.Application.PasswordRecovery;
using AtariGo.Server.Data.Entities;
using AtariGo.Server.Security;

namespace AtariGo.Server.Application.Authentication;

public sealed class LoginCodeProtector
{
    private readonly AuthenticationCodeProtector _protector;

    public LoginCodeProtector(IOptions<PasswordRecoveryOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _protector = new AuthenticationCodeProtector(options.Value.HmacKey);
    }

    public bool IsConfigured => _protector.IsConfigured;

    public string Protect(LoginChallenge challenge, string code)
    {
        ArgumentNullException.ThrowIfNull(challenge);
        return _protector.Protect(CreateScope(challenge), code);
    }

    public bool Matches(LoginChallenge challenge, string code, string expected)
    {
        ArgumentNullException.ThrowIfNull(challenge);
        return _protector.Matches(CreateScope(challenge), code, expected);
    }

    private static AuthenticationCodeScope CreateScope(LoginChallenge challenge)
    {
        return new AuthenticationCodeScope(challenge.UserId, LoginVerificationOptions.Purpose,
            challenge.TokenHash + ":" + challenge.CodeBinding);
    }
}
