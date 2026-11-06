using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace AtariGo.Server.Application.Rooms;

public sealed class RoomsService : IRoomsService
{
    private const int MinimumCaptureGoal = 1;
    private const int MaximumCaptureGoal = 3;
    private const int InvitationCodeByteCount = 5;
    private const int RoomLifetimeHours = 1;

    private static readonly TimeSpan s_roomLifetime = TimeSpan.FromHours(RoomLifetimeHours);

    private readonly object _roomLock = new object();
    private readonly Dictionary<int, RoomEntry> _rooms = new Dictionary<int, RoomEntry>();
    private readonly Dictionary<string, RoomEntry> _codes =
        new Dictionary<string, RoomEntry>(StringComparer.OrdinalIgnoreCase);
    private readonly TimeProvider _timeProvider;
    private int _lastRoomId;

    public RoomsService(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
    }

    public RoomOperationResult CreatePrivateRoom(
        LobbyParticipant host,
        LobbyConfiguration configuration)
    {
        if (!IsValidConfiguration(configuration))
        {
            return new RoomOperationResult(RoomOperationStatus.InvalidConfiguration);
        }

        lock (_roomLock)
        {
            RemoveExpiredRooms();
            RoomEntry? existing = _rooms.Values.FirstOrDefault(
                entry => entry.Snapshot.Host.UserId == host.UserId);
            if (existing is not null)
            {
                return ReuseRoom(existing.Snapshot, configuration);
            }

            var room = new PrivateRoom(
                checked(++_lastRoomId), CreateInvitationCode(), configuration)
            {
                Host = host
            };
            var entry = new RoomEntry(room, _timeProvider.GetUtcNow().Add(s_roomLifetime));
            _rooms.Add(room.RoomId, entry);
            _codes.Add(room.InvitationCode, entry);
            return new RoomOperationResult(RoomOperationStatus.Success, room);
        }
    }

    public RoomOperationResult JoinRoom(LobbyParticipant participant, RoomLocator locator)
    {
        lock (_roomLock)
        {
            RemoveExpiredRooms();
            RoomEntry? entry = FindRoom(locator);
            if (entry is null)
            {
                return new RoomOperationResult(RoomOperationStatus.NotFound);
            }

            PrivateRoom room = entry.Snapshot;
            bool isMember = IsMember(room, participant.UserId);
            if (!isMember && locator.RoomId > 0)
            {
                return new RoomOperationResult(RoomOperationStatus.NotMember);
            }

            if (!isMember && room.Opponent is not null)
            {
                return new RoomOperationResult(RoomOperationStatus.Full);
            }

            if (!isMember)
            {
                entry.Publish(room with { Opponent = participant, Version = room.Version + 1 });
            }

            return new RoomOperationResult(RoomOperationStatus.Success, entry.Snapshot);
        }
    }

    public RoomOperationResult LeaveRoom(int userId, int roomId)
    {
        lock (_roomLock)
        {
            RemoveExpiredRooms();
            if (!_rooms.TryGetValue(roomId, out RoomEntry? entry))
            {
                return new RoomOperationResult(RoomOperationStatus.NotFound);
            }

            PrivateRoom room = entry.Snapshot;
            if (!IsMember(room, userId))
            {
                return new RoomOperationResult(RoomOperationStatus.NotMember);
            }

            if (room.Host.UserId == userId)
            {
                CloseRoom(entry);
            }
            else
            {
                entry.Publish(room with { Opponent = null, Version = room.Version + 1 });
            }

            return new RoomOperationResult(RoomOperationStatus.Success, entry.Snapshot);
        }
    }

    public async IAsyncEnumerable<RoomOperationResult> WatchRoomAsync(
        RoomObservation observation,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        RoomEntry? entry = FindRoomById(observation.RoomId);
        if (entry is null)
        {
            yield return new RoomOperationResult(RoomOperationStatus.NotFound);
            yield break;
        }

        bool isWatching = true;
        while (isWatching)
        {
            RoomOperationResult result = await ReadRoomAsync(
                observation, entry, cancellationToken);
            yield return result;

            PrivateRoom? snapshot = result.Room;
            isWatching = snapshot is not null && !snapshot.IsClosed
                && IsMember(snapshot, observation.UserId);
            if (snapshot is not null)
            {
                observation = observation with { FromVersion = snapshot.Version };
            }
        }
    }

    private static RoomOperationResult ReuseRoom(
        PrivateRoom room,
        LobbyConfiguration configuration)
    {
        if (room.Configuration != configuration)
        {
            return new RoomOperationResult(RoomOperationStatus.AlreadyHosting);
        }

        return new RoomOperationResult(RoomOperationStatus.Success, room);
    }

    private RoomEntry? FindRoomById(int roomId)
    {
        lock (_roomLock)
        {
            RemoveExpiredRooms();
            _rooms.TryGetValue(roomId, out RoomEntry? entry);
            return entry;
        }
    }

    private async Task<RoomOperationResult> ReadRoomAsync(
        RoomObservation observation,
        RoomEntry entry,
        CancellationToken cancellationToken)
    {
        Task<PrivateRoom> nextSnapshot;
        lock (_roomLock)
        {
            PrivateRoom snapshot = entry.Snapshot;
            if (!IsMember(snapshot, observation.UserId))
            {
                return new RoomOperationResult(RoomOperationStatus.NotMember);
            }

            if (snapshot.Version != observation.FromVersion || snapshot.IsClosed)
            {
                return new RoomOperationResult(RoomOperationStatus.Success, snapshot);
            }

            nextSnapshot = entry.Changed.Task;
        }

        PrivateRoom changed = await WaitForSnapshotAsync(entry, nextSnapshot, cancellationToken);
        return new RoomOperationResult(RoomOperationStatus.Success, changed);
    }

    private static bool IsValidConfiguration(LobbyConfiguration configuration)
    {
        return configuration.CaptureGoal >= MinimumCaptureGoal
            && configuration.CaptureGoal <= MaximumCaptureGoal
            && configuration.TurnTimeSeconds > 0
            && configuration.GameTimeSeconds >= configuration.TurnTimeSeconds;
    }

    private static bool IsMember(PrivateRoom room, int userId)
    {
        return room.Host.UserId == userId || room.Opponent?.UserId == userId;
    }

    private RoomEntry? FindRoom(RoomLocator locator)
    {
        RoomEntry? entry;
        if (locator.RoomId > 0)
        {
            _rooms.TryGetValue(locator.RoomId, out entry);
        }
        else
        {
            _codes.TryGetValue(locator.InvitationCode.Trim(), out entry);
        }

        return entry;
    }

    private string CreateInvitationCode()
    {
        string code;
        do
        {
            code = Convert.ToHexString(RandomNumberGenerator.GetBytes(InvitationCodeByteCount));
        }
        while (_codes.ContainsKey(code));

        return code;
    }

    private async Task<PrivateRoom> WaitForSnapshotAsync(
        RoomEntry entry,
        Task<PrivateRoom> nextSnapshot,
        CancellationToken cancellationToken)
    {
        PrivateRoom? snapshot = null;
        TimeSpan remaining = entry.ExpiresAt - _timeProvider.GetUtcNow();
        if (remaining > TimeSpan.Zero)
        {
            try
            {
                snapshot = await nextSnapshot.WaitAsync(
                    remaining, _timeProvider, cancellationToken);
            }
            catch (TimeoutException)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        return snapshot ?? CloseExpiredRoom(entry, nextSnapshot);
    }

    private PrivateRoom CloseExpiredRoom(RoomEntry entry, Task<PrivateRoom> nextSnapshot)
    {
        lock (_roomLock)
        {
            if (!nextSnapshot.IsCompletedSuccessfully)
            {
                CloseRoom(entry);
            }

            return nextSnapshot.Result;
        }
    }

    private void RemoveExpiredRooms()
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        RoomEntry[] expiredRooms = _rooms.Values
            .Where(entry => entry.ExpiresAt <= now)
            .ToArray();
        foreach (RoomEntry entry in expiredRooms)
        {
            CloseRoom(entry);
        }
    }

    private void CloseRoom(RoomEntry entry)
    {
        PrivateRoom room = entry.Snapshot;
        _rooms.Remove(room.RoomId);
        _codes.Remove(room.InvitationCode);
        entry.Publish(room with { IsClosed = true, Version = room.Version + 1 });
    }
}
