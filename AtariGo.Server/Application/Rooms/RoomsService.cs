using System.Runtime.CompilerServices;
using System.Security.Cryptography;

using AtariGo.Server.Application.RoomInvitations;

namespace AtariGo.Server.Application.Rooms;

public sealed class RoomsService : IRoomsService
{
    private const int DefaultCaptureGoal = 1;
    private const int IntermediateCaptureGoal = 3;
    private const int ExtendedCaptureGoal = 5;
    private const int InvitationCodeByteCount = 5;
    private const int InvitationLifetimeMinutes = 2;
    private const int RoomLifetimeHours = 1;

    private static readonly TimeSpan roomLifetime = TimeSpan.FromHours(RoomLifetimeHours);

    private static readonly TimeSpan invitationLifetime =
        TimeSpan.FromMinutes(InvitationLifetimeMinutes);

    private readonly object _roomLock = new object();
    private readonly Dictionary<int, RoomEntry> _rooms = new Dictionary<int, RoomEntry>();
    private readonly Dictionary<string, RoomEntry> _codes =
        new Dictionary<string, RoomEntry>(StringComparer.OrdinalIgnoreCase);
    private readonly TimeProvider _timeProvider;
    private readonly RoomInvitationRegistry _invitations;
    private readonly IRoomInvitationNotifier _invitationNotifier;
    private int _lastRoomId;

    public RoomsService(
        TimeProvider timeProvider,
        IRoomInvitationNotifier invitationNotifier,
        ILogger<RoomsService> logger)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(invitationNotifier);
        ArgumentNullException.ThrowIfNull(logger);
        _timeProvider = timeProvider;
        _invitationNotifier = invitationNotifier;
        _invitations = new RoomInvitationRegistry(invitationNotifier, logger);
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

            string invitationCode = CreateInvitationCode();
            int roomId = checked(_lastRoomId + 1);
            var room = new PrivateRoom(
                roomId, invitationCode, configuration)
            {
                Host = host
            };
            var entry = new RoomEntry(room, _timeProvider.GetUtcNow().Add(roomLifetime));
            _rooms.Add(roomId, entry);
            try
            {
                _codes.Add(invitationCode, entry);
            }
            catch
            {
                _rooms.Remove(roomId);
                throw;
            }

            _lastRoomId = roomId;
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

            return JoinParticipant(entry, participant, null);
        }
    }

    public RoomInvitationResult SendRoomInvitation(
        SendRoomInvitationCommand command,
        LobbyParticipant recipient,
        int senderId)
    {
        lock (_roomLock)
        {
            RemoveExpiredRooms();
            if (!_rooms.TryGetValue(command.RoomId, out RoomEntry? room))
            {
                return new RoomInvitationResult(RoomInvitationOperationStatus.RoomUnavailable);
            }

            if (room.Snapshot.Host.UserId != senderId)
            {
                return new RoomInvitationResult(RoomInvitationOperationStatus.PermissionDenied);
            }

            if (!CanReceiveOpponent(room))
            {
                return new RoomInvitationResult(RoomInvitationOperationStatus.RoomUnavailable);
            }

            if (!_invitationNotifier.IsUserConnected(recipient.UserId))
            {
                return new RoomInvitationResult(RoomInvitationOperationStatus.RecipientOffline);
            }

            DateTimeOffset expiresAt = _timeProvider.GetUtcNow().Add(invitationLifetime);
            if (expiresAt > room.ExpiresAt)
            {
                expiresAt = room.ExpiresAt;
            }

            RoomInvitationEntry invitation = _invitations.Create(
                room.Snapshot, recipient, expiresAt);
            return new RoomInvitationResult(RoomInvitationOperationStatus.Success, invitation);
        }
    }

    public IReadOnlyList<RoomInvitationEntry> ListRoomInvitations(int userId, int? roomId)
    {
        lock (_roomLock)
        {
            RemoveExpiredRooms();
            return _invitations.List(userId, roomId);
        }
    }

    public RoomInvitationResult RespondRoomInvitation(
        RespondRoomInvitationCommand command,
        LobbyParticipant participant)
    {
        lock (_roomLock)
        {
            RemoveExpiredRooms();
            RoomInvitationEntry? invitation = _invitations.Find(command.InvitationId);
            if (invitation is null)
            {
                return new RoomInvitationResult(RoomInvitationOperationStatus.NotFound);
            }

            if (invitation.Recipient.UserId != participant.UserId)
            {
                return new RoomInvitationResult(RoomInvitationOperationStatus.PermissionDenied);
            }

            if (invitation.State == RoomInvitationStatus.Accepted && command.Accept)
            {
                return GetAcceptedInvitation(invitation, participant.UserId);
            }

            if (invitation.State != RoomInvitationStatus.Pending)
            {
                return CreateRepeatedDecisionResult(invitation, command.Accept);
            }

            if (!command.Accept)
            {
                RoomInvitationEntry rejected = _invitations.Transition(
                    invitation, RoomInvitationStatus.Rejected);
                return new RoomInvitationResult(RoomInvitationOperationStatus.Success, rejected);
            }

            return AcceptRoomInvitation(invitation, participant);
        }
    }

    public RoomInvitationResult CancelRoomInvitation(long invitationId, int senderId)
    {
        lock (_roomLock)
        {
            RemoveExpiredRooms();
            RoomInvitationEntry? invitation = _invitations.Find(invitationId);
            if (invitation is null)
            {
                return new RoomInvitationResult(RoomInvitationOperationStatus.NotFound);
            }

            if (invitation.Sender.UserId != senderId)
            {
                return new RoomInvitationResult(RoomInvitationOperationStatus.PermissionDenied);
            }

            if (invitation.State == RoomInvitationStatus.Cancelled)
            {
                return new RoomInvitationResult(RoomInvitationOperationStatus.Success, invitation);
            }

            if (invitation.State != RoomInvitationStatus.Pending)
            {
                return new RoomInvitationResult(
                    RoomInvitationOperationStatus.InvitationUnavailable);
            }

            RoomInvitationEntry cancelled = _invitations.Transition(
                invitation, RoomInvitationStatus.Cancelled);
            return new RoomInvitationResult(RoomInvitationOperationStatus.Success, cancelled);
        }
    }

    public RoomInvitationEntry? FindRoomInvitation(long invitationId, int recipientId)
    {
        lock (_roomLock)
        {
            RemoveExpiredRooms();
            RoomInvitationEntry? invitation = _invitations.Find(invitationId);
            return invitation?.Recipient.UserId == recipientId ? invitation : null;
        }
    }

    public void RemoveExpiredRoomsAndInvitations()
    {
        lock (_roomLock)
        {
            RemoveExpiredRooms();
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

    public RoomStartReservationResult ReserveStart(RoomStartRequest startRequest)
    {
        ArgumentNullException.ThrowIfNull(startRequest);
        lock (_roomLock)
        {
            RemoveExpiredRooms();
            if (!_rooms.TryGetValue(startRequest.RoomId, out RoomEntry? entry))
            {
                return new RoomStartReservationResult(RoomOperationStatus.NotFound);
            }

            PrivateRoom snapshot = entry.Snapshot;
            RoomOperationStatus status = ValidateStart(startRequest, entry);
            if (status != RoomOperationStatus.Success)
            {
                return new RoomStartReservationResult(status, snapshot);
            }

            if (snapshot.GameId is not null)
            {
                return new RoomStartReservationResult(status, snapshot);
            }

            Guid reservationId = Guid.NewGuid();
            entry.StartReservationId = reservationId;
            var reservation = new RoomStartReservation(reservationId, snapshot);
            return new RoomStartReservationResult(status, snapshot, reservation);
        }
    }

    public RoomOperationResult CompleteStart(
        RoomStartReservation reservation,
        long gameId)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(gameId);
        lock (_roomLock)
        {
            if (!_rooms.TryGetValue(reservation.Room.RoomId, out RoomEntry? entry))
            {
                return new RoomOperationResult(RoomOperationStatus.NotFound);
            }

            PrivateRoom currentRoom = entry.Snapshot;
            if (currentRoom.GameId == gameId
                && entry.CompletedStartReservationId == reservation.ReservationId)
            {
                return new RoomOperationResult(RoomOperationStatus.Success, currentRoom);
            }

            if (entry.StartReservationId != reservation.ReservationId
                || currentRoom != reservation.Room)
            {
                return new RoomOperationResult(RoomOperationStatus.StartInProgress, currentRoom);
            }

            PrivateRoom startedRoom = currentRoom with
            {
                GameId = gameId,
                Version = currentRoom.Version + 1
            };
            _invitations.Invalidate(currentRoom.RoomId);
            entry.CompletedStartReservationId = reservation.ReservationId;
            entry.StartReservationId = null;
            entry.Publish(startedRoom);
            return new RoomOperationResult(RoomOperationStatus.Success, startedRoom);
        }
    }

    public void CancelStart(RoomStartReservation reservation)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        lock (_roomLock)
        {
            if (!_rooms.TryGetValue(reservation.Room.RoomId, out RoomEntry? entry)
                || entry.StartReservationId != reservation.ReservationId)
            {
                return;
            }

            entry.StartReservationId = null;
            if (entry.ExpiresAt <= _timeProvider.GetUtcNow())
            {
                CloseRoom(entry);
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

    private RoomOperationResult JoinParticipant(
        RoomEntry entry,
        LobbyParticipant participant,
        long? acceptedInvitationId)
    {
        PrivateRoom room = entry.Snapshot;
        if (IsMember(room, participant.UserId))
        {
            return new RoomOperationResult(RoomOperationStatus.Success, room);
        }

        if (!CanReceiveOpponent(entry))
        {
            return new RoomOperationResult(RoomOperationStatus.Full);
        }

        bool hasAnotherRoom = _rooms.Values.Any(other => other.Snapshot.RoomId != room.RoomId
            && other.Snapshot.GameId is null && IsMember(other.Snapshot, participant.UserId));
        if (hasAnotherRoom)
        {
            return new RoomOperationResult(RoomOperationStatus.AlreadyHosting);
        }

        entry.Publish(room with { Opponent = participant, Version = room.Version + 1 });
        _invitations.Invalidate(room.RoomId, acceptedInvitationId);
        return new RoomOperationResult(RoomOperationStatus.Success, entry.Snapshot);
    }

    private static bool CanReceiveOpponent(RoomEntry entry)
    {
        PrivateRoom room = entry.Snapshot;
        return !entry.IsStarting && !room.IsClosed && room.GameId is null
            && room.Opponent is null;
    }

    private RoomInvitationResult AcceptRoomInvitation(
        RoomInvitationEntry invitation,
        LobbyParticipant participant)
    {
        if (!_rooms.TryGetValue(invitation.RoomId, out RoomEntry? entry))
        {
            return new RoomInvitationResult(RoomInvitationOperationStatus.RoomUnavailable);
        }

        RoomOperationResult joined = JoinParticipant(entry, participant, invitation.InvitationId);
        if (joined.Status == RoomOperationStatus.AlreadyHosting)
        {
            return new RoomInvitationResult(RoomInvitationOperationStatus.AlreadyInRoom);
        }

        if (joined.Status != RoomOperationStatus.Success)
        {
            _invitations.Transition(invitation, RoomInvitationStatus.Unavailable);
            return new RoomInvitationResult(RoomInvitationOperationStatus.RoomUnavailable);
        }

        RoomInvitationEntry accepted = _invitations.Transition(
            invitation, RoomInvitationStatus.Accepted);
        return new RoomInvitationResult(
            RoomInvitationOperationStatus.Success, accepted, joined.Room);
    }

    private RoomInvitationResult GetAcceptedInvitation(RoomInvitationEntry invitation, int userId)
    {
        if (_rooms.TryGetValue(invitation.RoomId, out RoomEntry? room)
            && IsMember(room.Snapshot, userId))
        {
            return new RoomInvitationResult(
                RoomInvitationOperationStatus.Success, invitation, room.Snapshot);
        }

        return new RoomInvitationResult(RoomInvitationOperationStatus.RoomUnavailable);
    }

    private static RoomInvitationResult CreateRepeatedDecisionResult(
        RoomInvitationEntry invitation,
        bool accept)
    {
        bool repeatedRejection = !accept && invitation.State == RoomInvitationStatus.Rejected;
        return new RoomInvitationResult(repeatedRejection
            ? RoomInvitationOperationStatus.Success
            : RoomInvitationOperationStatus.InvitationUnavailable, invitation);
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
            && configuration.GameTimeSeconds >= 0;
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
        _invitations.Expire(now);
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
        _invitations.Invalidate(room.RoomId);
        _rooms.Remove(room.RoomId);
        _codes.Remove(room.InvitationCode);
        entry.Publish(room with { IsClosed = true, Version = room.Version + 1 });
    }
}
