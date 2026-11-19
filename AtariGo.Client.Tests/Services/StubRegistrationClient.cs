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
            ArgumentNullException.ThrowIfNull(register);
            _register = register;
        }

        public RegistrationDraft? LastDraft { get; private set; }

        public int Calls { get; private set; }

        public Task<RegisterAccountResponse> RegisterAccountAsync(
            RegistrationDraft registrationDraft,
            CancellationToken cancellationToken = default)
        {
            LastDraft = registrationDraft;
            Calls++;
            return _register(cancellationToken);
        }
    }
}
