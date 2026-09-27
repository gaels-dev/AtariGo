using System.Threading;
using System.Threading.Tasks;
using AtariGo.Contracts;
using Grpc.Net.Client;

namespace AtariGo.Client.Services;

public sealed class GrpcAuthenticationClient : IAuthenticationClient
{
    private readonly GrpcChannel _channel = GrpcChannel.ForAddress("http://localhost:5026");
    private Authentication.AuthenticationClient? _client;

    public Task<LoginResponse> LoginAsync(
        string identifier,
        string password,
        CancellationToken cancellationToken = default)
    {
        _client ??= new Authentication.AuthenticationClient(_channel);

        return _client.LoginAsync(
            new LoginRequest { Identifier = identifier, Password = password },
            cancellationToken: cancellationToken).ResponseAsync;
    }
}
