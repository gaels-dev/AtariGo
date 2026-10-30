using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using Grpc.Core;

using AtariGo.Contracts;

namespace AtariGo.Client.Services
{
    public sealed class SessionPresenceMonitor
    {
        private readonly ISessionClient _sessionClient;
        private readonly TimeSpan _pollingInterval;

        public SessionPresenceMonitor(ISessionClient sessionClient, TimeSpan pollingInterval)
        {
            ArgumentNullException.ThrowIfNull(sessionClient);
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(pollingInterval, TimeSpan.Zero);

            _sessionClient = sessionClient;
            _pollingInterval = pollingInterval;
        }

        public event Action<HeartbeatResponse>? PresenceReceived;

        public event Action<RpcException>? RequestFailed;

        public event Action? SessionExpired;

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    bool shouldContinue = await RefreshAsync(cancellationToken);
                    if (!shouldContinue)
                    {
                        break;
                    }

                    await Task.Delay(_pollingInterval, cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Trace.TraceInformation("Presence monitor stopped by its owner.");
            }
        }

        private async Task<bool> RefreshAsync(CancellationToken cancellationToken)
        {
            bool shouldContinue = true;
            try
            {
                HeartbeatResponse response = await _sessionClient.HeartbeatAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                PresenceReceived?.Invoke(response);
            }
            catch (Exception exception)
                when (GrpcCallPolicy.IsRequestedCancellation(exception, cancellationToken))
            {
                Trace.TraceInformation("Pending heartbeat cancelled by its owner.");
                shouldContinue = false;
            }
            catch (RpcException exception)
            {
                Trace.TraceWarning("Presence request failed. RPC status: {0}.",
                    exception.StatusCode);
                if (exception.StatusCode == StatusCode.Unauthenticated)
                {
                    SessionExpired?.Invoke();
                    shouldContinue = false;
                }
                else
                {
                    RequestFailed?.Invoke(exception);
                }
            }

            return shouldContinue;
        }
    }
}
