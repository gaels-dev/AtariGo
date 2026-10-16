using System.Security.Cryptography;
using System.Text;

namespace AtariGo.Server.Security;

public sealed class SessionTokenProvider : ISessionTokenProvider
{
    public IssuedSessionToken Create()
    {
        string token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        string tokenHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(token)));

        return new IssuedSessionToken(token, tokenHash);
    }
}
