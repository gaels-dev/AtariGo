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
        string tokenHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(token)));

        return new IssuedSessionToken(token, tokenHash);
    }
}
