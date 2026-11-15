namespace AtariGo.Server.Application.Authentication;

public interface IAuthenticationService
{
    Task<LoginOperationResult> LoginAsync(
        string identifier,
        string password,
        CancellationToken cancellationToken);
}
