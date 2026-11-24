using Grpc.Core;

namespace AtariGo.Server.Tests.Games;

internal sealed class GameSessionTestCallContext : ServerCallContext
{
    private readonly Metadata _headers;
    private readonly CancellationToken _cancellation;

    public GameSessionTestCallContext(string credential,
        CancellationToken cancellationToken = default)
    {
        _cancellation = cancellationToken;
        _headers = new Metadata { { "x-guest-credential", credential } };
    }

    protected override string MethodCore => "GameSession.Connect";

    protected override string HostCore => "localhost";

    protected override string PeerCore => "test";

    protected override DateTime DeadlineCore => DateTime.MaxValue;

    protected override Metadata RequestHeadersCore => _headers;

    protected override CancellationToken CancellationTokenCore => _cancellation;

    protected override Metadata ResponseTrailersCore { get; } = new Metadata();

    protected override Status StatusCore
    {
        get; set;
    }

    protected override WriteOptions? WriteOptionsCore
    {
        get; set;
    }

    protected override AuthContext AuthContextCore => new AuthContext(null,
        new Dictionary<string, List<AuthProperty>>());

    protected override ContextPropagationToken CreatePropagationTokenCore(
        ContextPropagationOptions? options)
    {
        throw new NotSupportedException();
    }

    protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders)
    {
        return Task.CompletedTask;
    }
}
