using System.Threading;
using System.Threading.Tasks;

namespace AtariGo.Server.Application.Authentication;

public interface IRegistrationService
{
    Task<RegistrationStatus> RegisterAccountAsync(
        RegisterAccountCommand command,
        CancellationToken cancellationToken);
}
