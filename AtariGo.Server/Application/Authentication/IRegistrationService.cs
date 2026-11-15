
namespace AtariGo.Server.Application.Authentication;

public interface IRegistrationService
{
    Task<RegistrationStatus> RegisterAccountAsync(
        RegisterAccountCommand command,
        CancellationToken cancellationToken);
}
