namespace AtariGo.Server.Application.Authentication;

public interface IAuthenticationService
{
    Task<LoginOutcome> LoginAsync(
        string identifier,
        string password,
        CancellationToken cancellationToken);
}
