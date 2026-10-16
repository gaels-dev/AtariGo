namespace AtariGo.Server.Security;

public interface ISessionTokenProvider
{
    IssuedSessionToken Create();
}

public sealed record IssuedSessionToken(string Value, string Hash);
