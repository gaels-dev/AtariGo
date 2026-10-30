using AtariGo.Contracts;
using Grpc.Core;
using Grpc.Net.Client;

namespace AtariGo.Client.Services;

public sealed class GrpcSessionClient : ISessionClient
{
    private readonly string _sessionToken;
    private readonly GrpcChannel _channel;
    private readonly Session.SessionClient _client;

    public GrpcSessionClient(string sessionToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionToken);

        _sessionToken = sessionToken;
        _channel = GrpcChannel.ForAddress(GrpcClientSettings.ServerAddress);
        _client = new Session.SessionClient(_channel);
    }

    public Task<HeartbeatResponse> HeartbeatAsync(
        CancellationToken cancellationToken = default)
    {
        return _client.HeartbeatAsync(
            new HeartbeatRequest(),
            headers: CreateAuthorizationHeaders(),
            cancellationToken: cancellationToken).ResponseAsync;
    }

    public async Task LogoutAsync(
        CancellationToken cancellationToken = default)
    {
        await _client.LogoutAsync(
            new LogoutRequest(),
            headers: CreateAuthorizationHeaders(),
            cancellationToken: cancellationToken).ResponseAsync;
    }

    private Metadata CreateAuthorizationHeaders()
    {
        return new Metadata
        {
            { "authorization", $"Bearer {_sessionToken}" }
        };
    }
}
