namespace AtariGo.Server.Security;

public interface ISessionTokenProvider
{
    IssuedSessionToken Create();

    string Hash(string token);
}
