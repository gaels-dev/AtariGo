using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using AtariGo.Contracts.Validation;

namespace AtariGo.Server.Security;

public sealed class AuthenticationCodeProtector
{
    public const int CodeLength = VerificationCodeRules.Length;
    private const int MinimumKeyBytes = 32;
    private const int CodeUpperBound = 100_000_000;
    private readonly string _key;

    public AuthenticationCodeProtector(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        _key = key;
    }

    public bool IsConfigured => Encoding.UTF8.GetByteCount(_key) >= MinimumKeyBytes;

    public static string CreateCode()
    {
        return RandomNumberGenerator.GetInt32(CodeUpperBound)
            .ToString(new string('0', CodeLength), CultureInfo.InvariantCulture);
    }

    public string Protect(AuthenticationCodeScope scope, string code)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(code);
        if (!IsConfigured)
        {
            throw new InvalidOperationException("The authentication code key is not configured.");
        }
        byte[] key = Encoding.UTF8.GetBytes(_key);
        try
        {
            string value = CreatePayload(scope, code);
            return Convert.ToHexString(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(value)));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    public bool Matches(AuthenticationCodeScope scope, string code, string expected)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(expected);
        if (!VerificationCodeRules.IsValid(code))
        {
            return false;
        }
        byte[] actualBytes = Encoding.ASCII.GetBytes(Protect(scope, code));
        byte[] expectedBytes = Encoding.ASCII.GetBytes(expected);
        return CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes);
    }

    private static string CreatePayload(AuthenticationCodeScope scope, string code)
    {
        if (scope.Binding.Length == 0)
        {
            return FormattableString.Invariant($"{scope.UserId}:{scope.Purpose}:{code}");
        }
        string purpose = scope.Purpose;
        string identity = FormattableString.Invariant($"{scope.UserId}:{purpose.Length}:{purpose}");
        string binding = scope.Binding;
        return FormattableString.Invariant($"bound:{identity}:{binding.Length}:{binding}:{code}");
    }
}
