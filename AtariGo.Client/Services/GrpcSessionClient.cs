using System;
using System.Threading;
using System.Threading.Tasks;

using Grpc.Core;

using AtariGo.Contracts;

namespace AtariGo.Client.Services
{
    public sealed class GrpcSessionClient : ISessionClient
    {
        private readonly string _sessionToken;
        private readonly Session.SessionClient _client;

        public GrpcSessionClient(string sessionToken)
            : this(sessionToken, GrpcClientConnection.Shared.CallInvoker)
        {
        }

        public GrpcSessionClient(string sessionToken, CallInvoker callInvoker)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sessionToken);
            ArgumentNullException.ThrowIfNull(callInvoker);

            _sessionToken = sessionToken;
            _client = new Session.SessionClient(callInvoker);
        }

        public Task<HeartbeatResponse> HeartbeatAsync(
            CancellationToken cancellationToken = default)
        {
            return _client.HeartbeatAsync(
                new HeartbeatRequest(),
                GrpcCallPolicy.CreateOptions(
                    cancellationToken,
                    CreateAuthorizationHeaders())).ResponseAsync;
        }

        public async Task LogoutAsync(
            CancellationToken cancellationToken = default)
        {
            await _client.LogoutAsync(
                new LogoutRequest(),
                GrpcCallPolicy.CreateOptions(
                    cancellationToken,
                    CreateAuthorizationHeaders())).ResponseAsync;
        }

        private Metadata CreateAuthorizationHeaders()
        {
            return new Metadata
            {
                { "authorization", $"Bearer {_sessionToken}" }
            };
        }
    }
}
