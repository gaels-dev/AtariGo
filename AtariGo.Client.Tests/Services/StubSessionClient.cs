using System;
using System.Threading;
using System.Threading.Tasks;

using AtariGo.Client.Services;
using AtariGo.Contracts;

namespace AtariGo.Client.Tests.Services
{
    public sealed class StubSessionClient : ISessionClient
    {
        private readonly Func<CancellationToken, Task<HeartbeatResponse>> _heartbeat;

        public StubSessionClient(Func<CancellationToken, Task<HeartbeatResponse>> heartbeat)
        {
            _heartbeat = heartbeat;
        }

        public Task<HeartbeatResponse> HeartbeatAsync(CancellationToken cancellationToken = default)
        {
            return _heartbeat(cancellationToken);
        }

        public Task LogoutAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
