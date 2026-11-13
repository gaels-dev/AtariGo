
using AtariGo.Client.Models;
using AtariGo.Client.Services;
using AtariGo.Contracts;

namespace AtariGo.Client.Tests.Services
{
    public sealed class StubRegistrationClient : IRegistrationClient
    {
        private readonly Func<CancellationToken, Task<RegisterAccountResponse>> _register;

        public StubRegistrationClient(
            Func<CancellationToken, Task<RegisterAccountResponse>> register)
        {
            _register = register;
        }

        public Task<RegisterAccountResponse> RegisterAccountAsync(
            RegistrationDraft registrationDraft,
            string confirmationText,
            CancellationToken cancellationToken = default)
        {
            return _register(cancellationToken);
        }
    }
}
