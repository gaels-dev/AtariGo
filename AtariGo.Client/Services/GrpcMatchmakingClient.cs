using System.Runtime.CompilerServices;

using Grpc.Core;

using AtariGo.Contracts;
using AtariGo.Diagnostics;

namespace AtariGo.Client.Services;

public sealed class GrpcMatchmakingClient : IGuestIdentityClient, IMatchmakingClient
{
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
        ArgumentNullException.ThrowIfNull(displayName);
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
        ArgumentNullException.ThrowIfNull(guestCredential);
        Metadata headers = GrpcCallPolicy.CreateHeaders(null, guestCredential);
        await _client.RevokeGuestIdentityAsync(
            new RevokeGuestIdentityRequest(),
            GrpcCallPolicy.CreateOptions(cancellationToken, headers)).ResponseAsync;
    }

    public async IAsyncEnumerable<MatchmakingEvent> SearchPublicMatchAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        CallOptions options = new CallOptions(
            headers: GrpcCallPolicy.CreateHeaders(_sessionToken, _guestCredential),
            cancellationToken: cancellationToken);
        using AsyncServerStreamingCall<MatchmakingEvent> call =
            _client.SearchPublicMatch(new SearchPublicMatchRequest(), options);
        MatchmakingEvent.EventOneofCase previous = MatchmakingEvent.EventOneofCase.None;
        while (await ClientLobbyDiagnostics.MoveNextAsync(call.ResponseStream, cancellationToken))
        {
            MatchmakingEvent current = call.ResponseStream.Current;
            if (current.EventCase != previous)
            {
                ClientLobbyDiagnostics.Record(LobbyOperation.SearchRequest,
                    new DiagnosticEventDetails
                    {
                        Status = new DiagnosticStatus(current.EventCase),
                        Outcome = current.EventCase
                        == MatchmakingEvent.EventOneofCase.MatchCountdownCancelled
                        ? DiagnosticOutcome.Cancelled : DiagnosticOutcome.Succeeded
                    });
                previous = current.EventCase;
            }
            yield return current;
        }
    }
}
