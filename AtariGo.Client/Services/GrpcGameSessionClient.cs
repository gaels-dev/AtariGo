using Grpc.Core;

using AtariGo.Contracts;

namespace AtariGo.Client.Services;

public sealed class GrpcGameSessionClient : IGameSessionClient
{
    private readonly GameSession.GameSessionClient _client;
    private readonly string? _sessionToken;
    private readonly string? _guestCredential;

    public GrpcGameSessionClient(
        string? sessionToken,
        string? guestCredential)
        : this(sessionToken, guestCredential, GrpcClientConnection.Shared.CallInvoker)
    {
    }

    public GrpcGameSessionClient(
        string? sessionToken,
        string? guestCredential,
        CallInvoker callInvoker)
    {
        ArgumentNullException.ThrowIfNull(callInvoker);
        bool hasSessionToken = !string.IsNullOrWhiteSpace(sessionToken);
        bool hasGuestCredential = !string.IsNullOrWhiteSpace(guestCredential);
        if (hasSessionToken == hasGuestCredential)
        {
            throw new ArgumentException(
                "Exactly one game session credential must be configured.");
        }

        _sessionToken = sessionToken;
        _guestCredential = guestCredential;
        _client = new GameSession.GameSessionClient(callInvoker);
    }

    public async Task<IGameSessionStream> ConnectAsync(
        long gameId,
        CancellationToken cancellationToken)
    {
        Metadata headers = GrpcCallPolicy.CreateHeaders(_sessionToken, _guestCredential);
        CallOptions options = new CallOptions(
            headers: headers,
            cancellationToken: cancellationToken);
        AsyncDuplexStreamingCall<GameSessionClientEvent, GameSessionServerEvent> call =
            _client.Connect(options);
        bool isConnected = false;
        try
        {
            await call.RequestStream.WriteAsync(new GameSessionClientEvent
            {
                JoinGame = new JoinGame { GameId = gameId }
            });
            isConnected = true;
            return new GrpcGameSessionStream(call);
        }
        finally
        {
            if (!isConnected)
            {
                call.Dispose();
            }
        }
    }
}
