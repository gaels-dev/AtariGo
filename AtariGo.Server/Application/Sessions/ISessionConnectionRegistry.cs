namespace AtariGo.Server.Application.Sessions;

public interface ISessionConnectionRegistry
{
    SessionConnectionLease Register(SessionConnectionInfo connectionInfo);

    IReadOnlyList<FriendPresenceEntry> CreatePresenceSnapshot(
        SessionConnectionInfo connectionInfo);

    bool IsSessionConnected(long sessionId);

    void AddFriendship(
        int firstUserId,
        string firstUserName,
        int secondUserId,
        string secondUserName);

    bool Remove(SessionConnectionLease connection);

    void DisconnectSession(long sessionId);
}
