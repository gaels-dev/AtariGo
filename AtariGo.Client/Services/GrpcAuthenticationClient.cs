using System;
using System.Threading;
using System.Threading.Tasks;

using Grpc.Core;

using AtariGo.Contracts;

namespace AtariGo.Client.Services
{
    public sealed class GrpcAuthenticationClient : IAuthenticationClient
    {
        private readonly Authentication.AuthenticationClient _client;

        public GrpcAuthenticationClient()
            : this(GrpcClientConnection.Shared.CallInvoker)
        {
        }

        public GrpcAuthenticationClient(CallInvoker callInvoker)
        {
            ArgumentNullException.ThrowIfNull(callInvoker);
            _client = new Authentication.AuthenticationClient(callInvoker);
        }

        public Task<LoginResponse> LoginAsync(
            string identifier,
            string password,
            CancellationToken cancellationToken = default)
        {
            return _client.LoginAsync(
                new LoginRequest { Identifier = identifier, Password = password },
                GrpcCallPolicy.CreateOptions(cancellationToken)).ResponseAsync;
        }
    }
}
