using System.Collections.Concurrent;

using AtariGo.Contracts;

namespace AtariGo.Server.Application.Sessions;

public sealed class SessionConnectionRegistry : ISessionConnectionRegistry
{
    internal const int MaximumQueuedEvents = 128;

    private readonly ConcurrentDictionary<int, SessionConnectionLease> _connections =
        new ConcurrentDictionary<int, SessionConnectionLease>();
    private readonly object _registryLock = new object();

    public SessionConnectionLease Register(SessionConnectionInfo connectionInfo)
    {
        ArgumentNullException.ThrowIfNull(connectionInfo);
        var connection = new SessionConnectionLease(connectionInfo);

        lock (_registryLock)
        {
            ReplaceConnection(connection);
            NotifyFriends(connection, isOnline: true);
        }

        return connection;
    }

    public IReadOnlyList<FriendPresenceEntry> CreatePresenceSnapshot(
        SessionConnectionInfo connectionInfo)
    {
        ArgumentNullException.ThrowIfNull(connectionInfo);

        return connectionInfo.Friends
            .Select(friend => friend with
            {
                IsOnline = _connections.ContainsKey(friend.UserId)
            })
            .ToList();
    }

    public bool IsUserConnected(int userId)
    {
        return _connections.ContainsKey(userId);
    }

    public void NotifyRoomInvitationsChanged(int userId)
    {
        lock (_registryLock)
        {
            if (_connections.TryGetValue(userId, out SessionConnectionLease? connection))
            {
                var serverEvent = new SessionServerEvent
                {
                    RoomInvitationsChanged = new RoomInvitationsChanged()
                };
                if (!connection.OutboundEvents.Writer.TryWrite(serverEvent))
                {
                    connection.Stop();
                }
            }
        }
    }

    public bool IsSessionConnected(long sessionId)
    {
        return _connections.Values.Any(connection =>
            connection.ConnectionInfo.SessionId == sessionId);
    }

    public void AddFriendship(SessionIdentity firstUser, SessionIdentity secondUser)
    {
        ArgumentNullException.ThrowIfNull(firstUser);
        ArgumentNullException.ThrowIfNull(secondUser);
        lock (_registryLock)
        {
            bool isFirstOnline = _connections.ContainsKey(firstUser.UserId);
            bool isSecondOnline = _connections.ContainsKey(secondUser.UserId);
            AddFriendToConnection(
                firstUser.UserId,
                new FriendPresenceEntry(secondUser.UserId, secondUser.UserName, isSecondOnline));
            AddFriendToConnection(
                secondUser.UserId,
                new FriendPresenceEntry(firstUser.UserId, firstUser.UserName, isFirstOnline));
        }
    }

    public void RemoveFriendship(int firstUserId, int secondUserId)
    {
        lock (_registryLock)
        {
            RemoveFriendFromConnection(firstUserId, secondUserId);
            RemoveFriendFromConnection(secondUserId, firstUserId);
        }
    }

    public bool Remove(SessionConnectionLease connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        lock (_registryLock)
        {
            if (!IsCurrentConnection(connection))
            {
                connection.Stop();
                return false;
            }

            _connections.TryRemove(connection.ConnectionInfo.UserId, out _);
            connection.Stop();
            NotifyFriends(connection, isOnline: false);
            return true;
        }
    }

    public void DisconnectSession(long sessionId)
    {
        lock (_registryLock)
        {
            SessionConnectionLease? connection = _connections.Values
                .FirstOrDefault(candidate =>
                    candidate.ConnectionInfo.SessionId == sessionId);
            if (connection is null)
            {
                return;
            }

            _connections.TryRemove(connection.ConnectionInfo.UserId, out _);
            connection.Stop();
            NotifyFriends(connection, isOnline: false);
        }
    }

    private void ReplaceConnection(SessionConnectionLease connection)
    {
        int userId = connection.ConnectionInfo.UserId;
        if (_connections.TryGetValue(userId, out SessionConnectionLease? previous))
        {
            _connections[userId] = connection;
            previous.Stop();
            NotifyFormerFriends(previous, connection);
            return;
        }

        _connections.TryAdd(userId, connection);
    }

    private bool IsCurrentConnection(SessionConnectionLease connection)
    {
        return _connections.TryGetValue(
                connection.ConnectionInfo.UserId,
                out SessionConnectionLease? current)
            && ReferenceEquals(current, connection);
    }

    private void NotifyFriends(
        SessionConnectionLease connection,
        bool isOnline)
    {
        FriendPresenceEntry presence = CreateFriendPresence(
            connection,
            isOnline);
        foreach (FriendPresenceEntry friend in connection.ConnectionInfo.Friends)
        {
            bool hasFriendConnection = _connections.TryGetValue(
                friend.UserId,
                out SessionConnectionLease? friendConnection);
            if (!hasFriendConnection
                || friendConnection is null)
            {
                continue;
            }

            QueuePresenceEvent(friendConnection, presence);
        }
    }

    private void NotifyFormerFriends(
        SessionConnectionLease previous,
        SessionConnectionLease current)
    {
        HashSet<int> currentFriendIds = current.ConnectionInfo.Friends
            .Select(friend => friend.UserId)
            .ToHashSet();
        FriendPresenceEntry offlinePresence = CreateFriendPresence(
            previous,
            isOnline: false);
        foreach (FriendPresenceEntry friend in previous.ConnectionInfo.Friends)
        {
            if (currentFriendIds.Contains(friend.UserId)
                || !_connections.TryGetValue(
                    friend.UserId,
                    out SessionConnectionLease? friendConnection)
                || friendConnection is null)
            {
                continue;
            }

            QueuePresenceEvent(friendConnection, offlinePresence);
        }
    }

    private void AddFriendToConnection(
        int userId,
        FriendPresenceEntry friend)
    {
        if (!_connections.TryGetValue(userId, out SessionConnectionLease? connection)
            || connection is null
            || connection.ConnectionInfo.Friends.Any(
                existing => existing.UserId == friend.UserId))
        {
            return;
        }

        List<FriendPresenceEntry> friends = connection.ConnectionInfo.Friends
            .ToList();
        friends.Add(friend);
        connection.ConnectionInfo = connection.ConnectionInfo with
        {
            Friends = friends
        };
        QueuePresenceEvent(connection, friend);
    }

    private void RemoveFriendFromConnection(int userId, int friendUserId)
    {
        if (!_connections.TryGetValue(userId, out SessionConnectionLease? connection))
        {
            return;
        }

        connection.ConnectionInfo = connection.ConnectionInfo with
        {
            Friends = connection.ConnectionInfo.Friends
                .Where(friend => friend.UserId != friendUserId).ToList()
        };
        bool wasQueued = connection.OutboundEvents.Writer.TryWrite(new SessionServerEvent
        {
            FriendshipRemoved = new FriendshipRemoved { FriendUserId = friendUserId }
        });
        if (!wasQueued)
        {
            connection.Stop();
        }
    }

    private static void QueuePresenceEvent(
        SessionConnectionLease connection,
        FriendPresenceEntry friend)
    {
        bool wasQueued = connection.OutboundEvents.Writer.TryWrite(
            CreatePresenceChangedEvent(friend));
        if (!wasQueued)
        {
            connection.Stop();
        }
    }

    private static FriendPresenceEntry CreateFriendPresence(
        SessionConnectionLease connection,
        bool isOnline)
    {
        return new FriendPresenceEntry(
            connection.ConnectionInfo.UserId,
            connection.ConnectionInfo.UserName,
            isOnline);
    }

    private static SessionServerEvent CreatePresenceChangedEvent(
        FriendPresenceEntry friendPresence)
    {
        FriendPresence friend = new FriendPresence
        {
            UserId = friendPresence.UserId,
            UserName = friendPresence.UserName,
            IsOnline = friendPresence.IsOnline
        };

        return new SessionServerEvent
        {
            FriendPresenceChanged = new FriendPresenceChanged
            {
                Friend = friend
            }
        };
    }
}
