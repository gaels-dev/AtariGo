using System.Threading;
using System.Threading.Tasks;

using AtariGo.Client.Models;
using AtariGo.Contracts;

namespace AtariGo.Client.Services;

public interface IRegistrationClient
{
    Task<RegisterAccountResponse> RegisterAccountAsync(
        RegistrationDraft registrationDraft,
        string confirmationText,
        CancellationToken cancellationToken = default);
}
