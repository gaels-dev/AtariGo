
namespace AtariGo.Server.Application.Authentication;

public interface IRegistrationService
{
    /// <summary>Validates and persists a new player account.</summary>
    /// <param name="command">The account data submitted for registration.</param>
    /// <param name="cancellationToken">Cancels pending database operations.</param>
    /// <returns>The result of the registration operation.</returns>
    Task<RegistrationStatus> RegisterAccountAsync(
        RegisterAccountCommand command,
        CancellationToken cancellationToken);
}
