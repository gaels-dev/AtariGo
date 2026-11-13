using Grpc.Core;

using AtariGo.Contracts;

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
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
        }
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
        });

        while (await call.ResponseStream.MoveNext(cancellationToken))
        {
            callbacks.EventReceived(call.ResponseStream.Current);
        }
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

        callbacks.ConnectionFailed(exception);
        return true;
    }

    private CallOptions CreateRealtimeCallOptions(
        CancellationToken cancellationToken)
    {
        return new CallOptions(
            headers: CreateAuthorizationHeaders(),
            cancellationToken: cancellationToken);
    }

    private Metadata CreateAuthorizationHeaders()
    {
        return new Metadata
        {
            { "authorization", $"Bearer {_sessionToken}" }
        };
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
