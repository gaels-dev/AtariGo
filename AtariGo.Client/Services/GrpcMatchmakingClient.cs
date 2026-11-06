using System.Runtime.CompilerServices;

using Grpc.Core;

using AtariGo.Contracts;

namespace AtariGo.Client.Services;

public sealed class GrpcMatchmakingClient : IGuestIdentityClient, IMatchmakingClient
{
    private const string GuestCredentialHeader = "x-guest-credential";

    private readonly Matchmaking.MatchmakingClient _client;
    private readonly string? _sessionToken;
    private readonly string? _guestCredential;

    public GrpcMatchmakingClient(
        string? sessionToken = null,
        string? guestCredential = null)
        : this(sessionToken, guestCredential, GrpcClientConnection.Shared.CallInvoker)
    {
    }

    public GrpcMatchmakingClient(
        string? sessionToken,
        string? guestCredential,
        CallInvoker callInvoker)
    {
        ArgumentNullException.ThrowIfNull(callInvoker);
        if (!string.IsNullOrWhiteSpace(sessionToken)
            && !string.IsNullOrWhiteSpace(guestCredential))
        {
            throw new ArgumentException(
                "Only one matchmaking credential can be configured.");
        }

        _sessionToken = sessionToken;
        _guestCredential = guestCredential;
        _client = new Matchmaking.MatchmakingClient(callInvoker);
    }

    public async Task<string> CreateAsync(
        string displayName,
        CancellationToken cancellationToken)
    {
        var request = new CreateGuestIdentityRequest
        {
            DisplayName = displayName
        };
        CreateGuestIdentityResponse response = await _client.CreateGuestIdentityAsync(
            request,
            GrpcCallPolicy.CreateOptions(cancellationToken)).ResponseAsync;
        return response.Credential;
    }

    public async Task RevokeAsync(
        string guestCredential,
        CancellationToken cancellationToken)
    {
        var headers = new Metadata
        {
            { GuestCredentialHeader, guestCredential }
        };
        await _client.RevokeGuestIdentityAsync(
            new RevokeGuestIdentityRequest(),
            GrpcCallPolicy.CreateOptions(cancellationToken, headers)).ResponseAsync;
    }

    public async IAsyncEnumerable<MatchmakingEvent> SearchPublicMatchAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        CallOptions options = new CallOptions(
            headers: CreateHeaders(),
            cancellationToken: cancellationToken);
        using AsyncServerStreamingCall<MatchmakingEvent> call =
            _client.SearchPublicMatch(new SearchPublicMatchRequest(), options);
        while (await call.ResponseStream.MoveNext(cancellationToken))
        {
            yield return call.ResponseStream.Current;
        }
    }

    private Metadata CreateHeaders()
    {
        var headers = new Metadata();
        if (!string.IsNullOrWhiteSpace(_sessionToken))
        {
            headers.Add("authorization", $"Bearer {_sessionToken}");
        }

        if (!string.IsNullOrWhiteSpace(_guestCredential))
        {
            headers.Add(GuestCredentialHeader, _guestCredential);
        }

        return headers;
    }
}
