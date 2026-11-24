using AtariGo.Diagnostics;
using AtariGo.Server.Application.Rooms;
using AtariGo.Server.Diagnostics;

namespace AtariGo.Server.Application.RoomInvitations;

internal sealed class RoomInvitationRegistry
{
    private const int InvitationRetentionMinutes = 2;

    private static readonly TimeSpan invitationRetention =
        TimeSpan.FromMinutes(InvitationRetentionMinutes);

    private readonly Dictionary<long, RoomInvitationEntry> _entries =
        new Dictionary<long, RoomInvitationEntry>();
    private readonly IRoomInvitationNotifier _notifier;
    private readonly ILogger _logger;
    private long _lastId;

    public RoomInvitationRegistry(IRoomInvitationNotifier notifier, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(notifier);
        ArgumentNullException.ThrowIfNull(logger);
        _notifier = notifier;
        _logger = logger;
    }

    public RoomInvitationEntry Create(
        PrivateRoom room,
        LobbyParticipant recipient,
        DateTimeOffset expiresAt)
    {
        ArgumentNullException.ThrowIfNull(room);
        ArgumentNullException.ThrowIfNull(recipient);
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
        Record(entry, LobbyOperation.InvitationCreated);
        Notify(entry);
        return entry;
    }

    public RoomInvitationEntry? Find(long invitationId)
    {
        _entries.TryGetValue(invitationId, out RoomInvitationEntry? entry);
        return entry;
    }

    public RoomInvitationEntry ExtendExpiration(RoomInvitationEntry entry, DateTimeOffset expiresAt)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (expiresAt <= entry.ExpiresAt)
        {
            return entry;
        }
        RoomInvitationEntry updated = entry with
        {
            ExpiresAt = expiresAt
        };
        _entries[entry.InvitationId] = updated;
        Notify(updated);
        return updated;
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
        ArgumentNullException.ThrowIfNull(entry);
        RoomInvitationEntry updated = entry with
        {
            State = nextStatus
        };
        _entries[entry.InvitationId] = updated;
        if (entry.State != nextStatus)
        {
            Record(updated, LobbyOperation.InvitationChanged);
        }
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

            if (entry.ExpiresAt.Add(invitationRetention) <= currentTime)
            {
                _entries.Remove(entry.InvitationId);
                Notify(entry);
            }
        }
    }

    private void Record(RoomInvitationEntry entry, LobbyOperation operation)
    {
        ServerLobbyDiagnostics.Record(_logger, operation, new DiagnosticEventDetails
        {
            Outcome = DiagnosticOutcome.Succeeded,
            Status = new DiagnosticStatus(entry.State),
            Context = new DiagnosticContext
            {
                InvitationId = entry.InvitationId,
                RoomId = entry.RoomId
            }
        });
    }

    private void Notify(RoomInvitationEntry entry)
    {
        _notifier.NotifyRoomInvitationsChanged(entry.Sender.UserId);
        _notifier.NotifyRoomInvitationsChanged(entry.Recipient.UserId);
    }
}
