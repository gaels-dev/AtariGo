using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace AtariGo.Server.Application.Rooms;

public sealed class RoomsService : IRoomsService
{
    private const int DefaultCaptureGoal = 1;
    private const int IntermediateCaptureGoal = 3;
    private const int ExtendedCaptureGoal = 5;
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
                entry => entry.Snapshot.Host.UserId == host.UserId
                    && entry.Snapshot.GameId is null);
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

    public RoomOperationResult UpdateRoomConfiguration(
        UpdateRoomConfigurationCommand command,
        int userId)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Configuration);
        lock (_roomLock)
        {
            RemoveExpiredRooms();
            if (!_rooms.TryGetValue(command.RoomId, out RoomEntry? entry))
            {
                return new RoomOperationResult(RoomOperationStatus.NotFound);
            }

            RoomOperationStatus status = ValidateConfigurationUpdate(command, userId, entry);
            if (status != RoomOperationStatus.Success)
            {
                return new RoomOperationResult(status);
            }

            PrivateRoom room = entry.Snapshot;
            if (room.Configuration != command.Configuration)
            {
                entry.Publish(room with
                {
                    Configuration = command.Configuration,
                    Version = room.Version + 1
                });
            }

            return new RoomOperationResult(RoomOperationStatus.Success, entry.Snapshot);
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

            if (!isMember && (room.Opponent is not null || room.GameId is not null))
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

            if (entry.IsStarting || room.GameId is not null)
            {
                return new RoomOperationResult(RoomOperationStatus.StartInProgress);
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

    public async Task<RoomOperationResult> StartGameAsync(
        RoomStartRequest request,
        Func<PrivateRoom, CancellationToken, Task<long>> persistGameAsync,
        CancellationToken cancellationToken)
    {
        RoomEntry entry;
        PrivateRoom snapshot;
        lock (_roomLock)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RemoveExpiredRooms();
            if (!_rooms.TryGetValue(request.RoomId, out RoomEntry? currentEntry))
            {
                return new RoomOperationResult(RoomOperationStatus.NotFound);
            }

            entry = currentEntry;
            snapshot = entry.Snapshot;
            RoomOperationStatus status = ValidateStart(request, entry);
            if (status != RoomOperationStatus.Success || snapshot.GameId is not null)
            {
                return new RoomOperationResult(status, snapshot);
            }

            entry.IsStarting = true;
        }

        try
        {
            // Once reserved, the start must resolve even if the caller disconnects.
            long gameId = await persistGameAsync(snapshot, CancellationToken.None);
            lock (_roomLock)
            {
                entry.Publish(snapshot with { GameId = gameId, Version = snapshot.Version + 1 });
                return new RoomOperationResult(RoomOperationStatus.Success, entry.Snapshot);
            }
        }
        finally
        {
            lock (_roomLock)
            {
                entry.IsStarting = false;
                if (entry.Snapshot.GameId is null && entry.ExpiresAt <= _timeProvider.GetUtcNow())
                {
                    CloseRoom(entry);
                }
            }
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
            isWatching = snapshot is not null && !snapshot.IsClosed && snapshot.GameId is null
                && IsMember(snapshot, observation.UserId);
            if (snapshot is not null)
            {
                observation = observation with { FromVersion = snapshot.Version };
            }
        }
    }

    private static RoomOperationStatus ValidateConfigurationUpdate(
        UpdateRoomConfigurationCommand command,
        int userId,
        RoomEntry entry)
    {
        PrivateRoom room = entry.Snapshot;
        if (room.Host.UserId != userId)
        {
            return RoomOperationStatus.HostRequired;
        }

        if (room.IsClosed || entry.IsStarting || room.GameId is not null)
        {
            return RoomOperationStatus.StartInProgress;
        }

        if (room.Version != command.ExpectedVersion)
        {
            return RoomOperationStatus.VersionMismatch;
        }

        return IsValidConfiguration(command.Configuration)
            ? RoomOperationStatus.Success
            : RoomOperationStatus.InvalidConfiguration;
    }

    private static RoomOperationStatus ValidateStart(RoomStartRequest request, RoomEntry entry)
    {
        PrivateRoom room = entry.Snapshot;
        if (room.Host.UserId != request.UserId)
        {
            return RoomOperationStatus.NotMember;
        }

        if (room.GameId is not null)
        {
            return RoomOperationStatus.Success;
        }

        if (entry.IsStarting)
        {
            return RoomOperationStatus.StartInProgress;
        }

        if (room.Version != request.ExpectedVersion)
        {
            return RoomOperationStatus.VersionMismatch;
        }

        if (room.Opponent is null)
        {
            return RoomOperationStatus.CannotStart;
        }

        return RoomOperationStatus.Success;
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

            if (snapshot.Version != observation.FromVersion || snapshot.IsClosed
                || snapshot.GameId is not null)
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
        return configuration.CaptureGoal is
            (DefaultCaptureGoal or IntermediateCaptureGoal or ExtendedCaptureGoal)
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

        return snapshot ?? await CloseExpiredRoom(entry, nextSnapshot)
            .WaitAsync(cancellationToken);
    }

    private Task<PrivateRoom> CloseExpiredRoom(RoomEntry entry, Task<PrivateRoom> nextSnapshot)
    {
        lock (_roomLock)
        {
            if (!nextSnapshot.IsCompletedSuccessfully && !entry.IsStarting)
            {
                CloseRoom(entry);
            }

            return nextSnapshot;
        }
    }

    private void RemoveExpiredRooms()
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        RoomEntry[] expiredRooms = _rooms.Values
            .Where(entry => !entry.IsStarting && entry.ExpiresAt <= now)
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
