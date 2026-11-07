using AtariGo.Contracts;

namespace AtariGo.Client.Services;

public interface IFriendshipsClient
{
    Task<RemoveFriendshipResponse> RemoveFriendshipAsync(
        int friendUserId,
        CancellationToken cancellationToken = default);
}
