namespace AtariGo.Server.Security;

public interface ISessionTokenProvider
{
    IssuedSessionToken Create();

    string Hash(string token);
}

public sealed record IssuedSessionToken(string Value, string Hash);
