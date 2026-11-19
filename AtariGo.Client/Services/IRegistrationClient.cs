using AtariGo.Client.Models;
using AtariGo.Contracts;

namespace AtariGo.Client.Services;

public interface IRegistrationClient
{
    Task<RegisterAccountResponse> RegisterAccountAsync(
        RegistrationDraft registrationDraft,
        CancellationToken cancellationToken = default);
}
