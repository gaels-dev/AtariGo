using AtariGo.Server.Application.Rooms;

namespace AtariGo.Server.Application.RoomInvitations;

// RoomsService owns this registry and calls it only while holding its room lock.
internal sealed class RoomInvitationRegistry
{
    private const int InvitationRetentionMinutes = 2;

    private static readonly TimeSpan s_invitationRetention =
        TimeSpan.FromMinutes(InvitationRetentionMinutes);

    private readonly Dictionary<long, RoomInvitationEntry> _entries =
        new Dictionary<long, RoomInvitationEntry>();
    private readonly IRoomInvitationNotifier _notifier;
    private readonly ILogger _logger;
    private long _lastId;

    public RoomInvitationRegistry(IRoomInvitationNotifier notifier, ILogger logger)
    {
        _notifier = notifier;
        _logger = logger;
    }

    public RoomInvitationEntry Create(
        PrivateRoom room,
        LobbyParticipant recipient,
        DateTimeOffset expiresAt)
    {
        RoomInvitationEntry? pending = _entries.Values.FirstOrDefault(invitation =>
            invitation.RoomId == room.RoomId && invitation.Recipient.UserId == recipient.UserId
            && invitation.State == RoomInvitationStatus.Pending);
        if (pending is not null)
        {
            return pending;
        }

        var entry = new RoomInvitationEntry(checked(++_lastId), room.RoomId, expiresAt)
        {
            RoomCode = room.InvitationCode,
            Sender = room.Host,
            Recipient = recipient
        };
        _entries.Add(entry.InvitationId, entry);
        Notify(entry);
        return entry;
    }

    public RoomInvitationEntry? Find(long invitationId)
    {
        _entries.TryGetValue(invitationId, out RoomInvitationEntry? entry);
        return entry;
    }

    public IReadOnlyList<RoomInvitationEntry> List(int userId, int? roomId)
    {
        return _entries.Values.Where(entry => roomId is null
                ? entry.Recipient.UserId == userId
                : entry.RoomId == roomId.Value && entry.Sender.UserId == userId)
            .OrderByDescending(entry => entry.InvitationId).ToArray();
    }

    public RoomInvitationEntry Transition(
        RoomInvitationEntry entry,
        RoomInvitationStatus nextStatus)
    {
        RoomInvitationEntry updated = entry with { State = nextStatus };
        _entries[entry.InvitationId] = updated;
        Notify(updated);
        return updated;
    }

    public void Invalidate(int roomId, long? acceptedInvitationId = null)
    {
        RoomInvitationEntry[] pending = _entries.Values.Where(entry => entry.RoomId == roomId
            && entry.InvitationId != acceptedInvitationId
            && entry.State == RoomInvitationStatus.Pending).ToArray();
        foreach (RoomInvitationEntry entry in pending)
        {
            Transition(entry, RoomInvitationStatus.Unavailable);
        }
    }

    public void Expire(DateTimeOffset currentTime)
    {
        RoomInvitationEntry[] expired = _entries.Values
            .Where(entry => entry.ExpiresAt <= currentTime).ToArray();
        foreach (RoomInvitationEntry entry in expired)
        {
            if (entry.State == RoomInvitationStatus.Pending)
            {
                Transition(entry, RoomInvitationStatus.Expired);
            }

            if (entry.ExpiresAt.Add(s_invitationRetention) <= currentTime)
            {
                _entries.Remove(entry.InvitationId);
                Notify(entry);
            }
        }
    }

    private void Notify(RoomInvitationEntry entry)
    {
        _logger.LogInformation(
            "Room invitation {InvitationId} in room {RoomId}: {State}.",
            entry.InvitationId, entry.RoomId, entry.State);
        // The notifier only queues events; no network waits occur under the room lock.
        _notifier.NotifyRoomInvitationsChanged(entry.Sender.UserId);
        _notifier.NotifyRoomInvitationsChanged(entry.Recipient.UserId);
    }
}
