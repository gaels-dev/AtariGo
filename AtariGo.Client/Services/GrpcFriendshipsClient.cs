using Grpc.Core;

using AtariGo.Contracts;
using AtariGo.Diagnostics;

namespace AtariGo.Client.Services;

public sealed class GrpcFriendshipsClient : IFriendshipsClient
{
    private readonly string _sessionToken;
    private readonly Friendships.FriendshipsClient _client;

    public GrpcFriendshipsClient(string sessionToken)
        : this(sessionToken, GrpcClientConnection.Shared.CallInvoker)
    {
    }

    public GrpcFriendshipsClient(string sessionToken, CallInvoker callInvoker)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionToken);
        ArgumentNullException.ThrowIfNull(callInvoker);
        _sessionToken = sessionToken;
        _client = new Friendships.FriendshipsClient(callInvoker);
    }

    public async Task<RemoveFriendshipResponse> RemoveFriendshipAsync(
        int friendUserId,
        CancellationToken cancellationToken = default)
    {
        var request = new RemoveFriendshipRequest { FriendUserId = friendUserId };
        CallOptions options = GrpcCallPolicy.CreateOptions(cancellationToken,
            GrpcCallPolicy.CreateHeaders(_sessionToken));
        RemoveFriendshipResponse response = await GrpcCallPolicy.ExecuteAsync(() =>
            _client.RemoveFriendshipAsync(request, options).ResponseAsync);
        ClientIdentityDiagnostics.Completed(IdentityOperation.RemoveFriendship, response.Status);
        return response;
    }
}
