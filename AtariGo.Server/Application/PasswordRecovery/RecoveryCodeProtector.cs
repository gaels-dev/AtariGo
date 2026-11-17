using Microsoft.Extensions.Options;

using AtariGo.Server.Security;

namespace AtariGo.Server.Application.PasswordRecovery;

public sealed class RecoveryCodeProtector
{
    public const string Purpose = "PasswordReset";
    public const int MaximumAttempts = 5;
    public const int CodeLength = AuthenticationCodeProtector.CodeLength;
    private readonly AuthenticationCodeProtector _protector;

    public RecoveryCodeProtector(IOptions<PasswordRecoveryOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _protector = new AuthenticationCodeProtector(options.Value.HmacKey);
    }

    public static TimeSpan Lifetime { get; } = TimeSpan.FromMinutes(10);

    public bool IsConfigured => _protector.IsConfigured;

    public static string CreateCode()
    {
        return AuthenticationCodeProtector.CreateCode();
    }

    public string Protect(int userId, string code)
    {
        return _protector.Protect(new AuthenticationCodeScope(userId, Purpose), code);
    }

    public bool Matches(int userId, string code, string expected)
    {
        return _protector.Matches(new AuthenticationCodeScope(userId, Purpose), code, expected);
    }
}
