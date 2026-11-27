using AtariGo.Server.Application.RoomInvitations;
using AtariGo.Server.Concurrency;

namespace AtariGo.Server.Application.Sessions;

public interface ISessionConnectionRegistry : IRoomInvitationNotifier
{
    Task<AsyncOperationLease> EnterAccessAsync(int userId, CancellationToken cancellationToken);

    SessionConnectionLease? Register(SessionConnectionInfo connectionInfo);

    IReadOnlyList<FriendPresenceEntry> CreatePresenceSnapshot(
        SessionConnectionInfo connectionInfo);

    bool IsSessionConnected(long sessionId);

    void AddFriendship(SessionIdentity firstUser, SessionIdentity secondUser);

    void RemoveFriendship(int firstUserId, int secondUserId);

    void UpdateUserName(int userId, string userName);

    bool Remove(SessionConnectionLease connection);

    void DisconnectSession(long sessionId);
}
