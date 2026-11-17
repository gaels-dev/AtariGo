using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using Microsoft.Extensions.Options;

namespace AtariGo.Server.Application.PasswordRecovery;

public sealed class RecoveryCodeProtector
{
    public const string Purpose = "PasswordReset";
    public const int MaximumAttempts = 5;
    public const int CodeLength = 8;
    private const int MinimumKeyBytes = 32;
    private const int CodeUpperBound = 100_000_000;
    public static TimeSpan Lifetime { get; } = TimeSpan.FromMinutes(10);
    private readonly PasswordRecoveryOptions _options;

    public RecoveryCodeProtector(IOptions<PasswordRecoveryOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    public bool IsConfigured => Encoding.UTF8.GetByteCount(_options.HmacKey) >= MinimumKeyBytes;

    public static string CreateCode()
    {
        return RandomNumberGenerator.GetInt32(CodeUpperBound)
            .ToString(new string('0', CodeLength), CultureInfo.InvariantCulture);
    }

    public string Protect(int userId, string code)
    {
        byte[] key = Encoding.UTF8.GetBytes(_options.HmacKey);
        try
        {
            string value = FormattableString.Invariant($"{userId}:{Purpose}:{code}");
            return Convert.ToHexString(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(value)));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    public bool Matches(int userId, string code, string expected)
    {
        if (code.Length != CodeLength || code.Any(character => character is < '0' or > '9'))
        {
            return false;
        }
        byte[] actualBytes = Encoding.ASCII.GetBytes(Protect(userId, code));
        byte[] expectedBytes = Encoding.ASCII.GetBytes(expected);
        return CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes);
    }
}
