using AtariGo.Client.Services;
using AtariGo.Contracts;

namespace AtariGo.Client.Tests.Services;

public sealed class StubFriendshipsClient : IFriendshipsClient
{
    public Task<RemoveFriendshipResponse> Response { get; set; } = Task.FromResult(
        new RemoveFriendshipResponse { Status = RemoveFriendshipStatus.Removed });

    public int Calls { get; private set; }

    public int FriendUserId { get; private set; }

    public CancellationToken RequestToken { get; private set; }

    public Task<RemoveFriendshipResponse> RemoveFriendshipAsync(
        int friendUserId,
        CancellationToken cancellationToken = default)
    {
        Calls++;
        FriendUserId = friendUserId;
        RequestToken = cancellationToken;
        return Response;
    }
}
