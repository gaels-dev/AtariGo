using System;
using System.Threading;
using System.Threading.Tasks;

using AtariGo.Client.Services;
using AtariGo.Contracts;

namespace AtariGo.Client.Tests.Services
{
    public sealed class StubAuthenticationClient : IAuthenticationClient
    {
        private readonly Func<CancellationToken, Task<LoginResponse>> _login;

        public StubAuthenticationClient(Func<CancellationToken, Task<LoginResponse>> login)
        {
            _login = login;
        }

        public Task<LoginResponse> LoginAsync(
            string identifier,
            string password,
            CancellationToken cancellationToken = default)
        {
            return _login(cancellationToken);
        }
    }
}
