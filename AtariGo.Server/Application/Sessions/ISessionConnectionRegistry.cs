using AtariGo.Server.Application.RoomInvitations;

namespace AtariGo.Server.Application.Sessions;

public interface ISessionConnectionRegistry : IRoomInvitationNotifier
{
    SessionConnectionLease Register(SessionConnectionInfo connectionInfo);

    IReadOnlyList<FriendPresenceEntry> CreatePresenceSnapshot(
        SessionConnectionInfo connectionInfo);

    bool IsSessionConnected(long sessionId);

    void AddFriendship(SessionIdentity firstUser, SessionIdentity secondUser);

    void RemoveFriendship(int firstUserId, int secondUserId);

    bool Remove(SessionConnectionLease connection);

    void DisconnectSession(long sessionId);
}
