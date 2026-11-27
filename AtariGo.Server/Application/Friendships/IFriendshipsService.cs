namespace AtariGo.Server.Application.Friendships;

public interface IFriendshipsService
{
    Task<RemoveFriendshipResult?> RemoveFriendshipAsync(
        string sessionToken,
        int friendUserId,
        CancellationToken cancellationToken);
}
