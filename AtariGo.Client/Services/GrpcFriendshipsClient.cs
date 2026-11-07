using Grpc.Core;

using AtariGo.Contracts;

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

    public Task<RemoveFriendshipResponse> RemoveFriendshipAsync(
        int friendUserId,
        CancellationToken cancellationToken = default)
    {
        return _client.RemoveFriendshipAsync(
            new RemoveFriendshipRequest { FriendUserId = friendUserId },
            GrpcCallPolicy.CreateOptions(cancellationToken,
                new Metadata { { "authorization", $"Bearer {_sessionToken}" } })).ResponseAsync;
    }
}
