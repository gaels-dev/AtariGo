namespace AtariGo.Server.Application.Authentication;

public interface IAuthenticationService
{
    Task<LoginOutcome> LoginAsync(
        string identifier,
        string password,
        CancellationToken cancellationToken);

    Task<RegistrationStatus> RegisterAccountAsync(
        RegisterAccountCommand command,
        CancellationToken cancellationToken);
}
