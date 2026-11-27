using System.Security.Cryptography;
using System.Text;

namespace AtariGo.Server.Security;

public sealed class SessionTokenProvider : ISessionTokenProvider
{
    private const int TokenByteLength = 32;

    public IssuedSessionToken Create()
    {
        string token = Convert.ToBase64String(
            RandomNumberGenerator.GetBytes(TokenByteLength));
        string tokenHash = Hash(token);

        return new IssuedSessionToken(token, tokenHash);
    }

    public string Hash(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
