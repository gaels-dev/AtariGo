using Grpc.Core;

using AtariGo.Contracts;
using AtariGo.Diagnostics;

namespace AtariGo.Client.Services;

public sealed class GrpcSessionClient : ISessionClient
{
    private static readonly TimeSpan initialReconnectDelay =
        TimeSpan.FromSeconds(1);
    private static readonly TimeSpan maximumReconnectDelay =
        TimeSpan.FromSeconds(5);
    private const int ReconnectDelayMultiplier = 2;

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

    public async Task RunRealtimeAsync(
        SessionRealtimeCallbacks callbacks,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(callbacks);
        TimeSpan reconnectDelay = initialReconnectDelay;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                bool shouldReconnect = await ConnectAndReceiveAsync(
                    callbacks,
                    cancellationToken);
                if (!shouldReconnect)
                {
                    return;
                }

                await Task.Delay(reconnectDelay, cancellationToken);
                reconnectDelay = GetNextReconnectDelay(reconnectDelay);
            }
        }
        catch (OperationCanceledException exception)
            when (cancellationToken.IsCancellationRequested)
        {
            ClientIdentityDiagnostics.Failure(IdentityOperation.PresenceDisconnected, exception,
                cancellationToken);
        }
    }

    public async Task LogoutAsync(
        CancellationToken cancellationToken = default)
    {
        await GrpcCallPolicy.ExecuteAsync(() => _client.LogoutAsync(
            new LogoutRequest(),
            GrpcCallPolicy.CreateOptions(
                cancellationToken,
                GrpcCallPolicy.CreateHeaders(_sessionToken))).ResponseAsync);
        ClientIdentityDiagnostics.Record(IdentityOperation.Logout,
            new DiagnosticEventDetails { Outcome = DiagnosticOutcome.Stopped });
    }

    private async Task<bool> ConnectAndReceiveAsync(
        SessionRealtimeCallbacks callbacks,
        CancellationToken cancellationToken)
    {
        try
        {
            await ReceiveConnectionEventsAsync(callbacks, cancellationToken);
            return true;
        }
        catch (RpcException exception)
        {
            ClientIdentityDiagnostics.Failure(IdentityOperation.PresenceDisconnected, exception,
                cancellationToken);
            return HandleConnectionFailure(exception, callbacks);
        }
    }

    private async Task ReceiveConnectionEventsAsync(
        SessionRealtimeCallbacks callbacks,
        CancellationToken cancellationToken)
    {
        using AsyncDuplexStreamingCall<SessionClientEvent, SessionServerEvent> call =
            _client.Connect(CreateRealtimeCallOptions(cancellationToken));
        await call.RequestStream.WriteAsync(new SessionClientEvent
        {
            ClientConnected = new ClientConnected()
        }, cancellationToken);

        bool connected = false;
        while (await call.ResponseStream.MoveNext(cancellationToken))
        {
            if (!connected)
            {
                ClientIdentityDiagnostics.Record(IdentityOperation.PresenceConnected,
                    new DiagnosticEventDetails { Outcome = DiagnosticOutcome.Started });
                connected = true;
            }
            callbacks.EventReceived(call.ResponseStream.Current);
        }
        ClientIdentityDiagnostics.Record(IdentityOperation.PresenceDisconnected,
            new DiagnosticEventDetails { Outcome = DiagnosticOutcome.Stopped });
    }

    private bool HandleConnectionFailure(
        RpcException exception,
        SessionRealtimeCallbacks callbacks)
    {
        if (exception.StatusCode == StatusCode.Unauthenticated)
        {
            callbacks.SessionExpired();
            return false;
        }

        callbacks.ConnectionFailed(GrpcClientFailures.GetReason(exception));
        return true;
    }

    private CallOptions CreateRealtimeCallOptions(
        CancellationToken cancellationToken)
    {
        return new CallOptions(
            headers: GrpcCallPolicy.CreateHeaders(_sessionToken),
            cancellationToken: cancellationToken);
    }

    private static TimeSpan GetNextReconnectDelay(TimeSpan currentDelay)
    {
        TimeSpan nextDelay = TimeSpan.FromTicks(
            currentDelay.Ticks * ReconnectDelayMultiplier);
        return nextDelay <= maximumReconnectDelay
            ? nextDelay
            : maximumReconnectDelay;
    }
}
