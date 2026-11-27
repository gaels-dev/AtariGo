namespace AtariGo.Server.Application.RoomInvitations;

public sealed class RoomInvitationEmailLimiter
{
    private const int ResendDelaySeconds = 60;
    private const int MaximumRequestsPerHour = 10;
    private const int MaximumTrackedSenders = 10000;
    private static readonly TimeSpan requestWindow = TimeSpan.FromHours(1);

    private readonly object _gate = new object();
    private readonly Dictionary<int, Queue<DateTimeOffset>> _requests = new();
    private readonly Dictionary<(int Sender, int Room, int Recipient), DateTimeOffset> _recent =
        new Dictionary<(int Sender, int Room, int Recipient), DateTimeOffset>();
    private readonly HashSet<long> _sending = new HashSet<long>();
    private readonly TimeProvider _timeProvider;

    public RoomInvitationEmailLimiter(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
    }

    public int TryReserve(RoomInvitationEntry invitation)
    {
        ArgumentNullException.ThrowIfNull(invitation);
        lock (_gate)
        {
            DateTimeOffset now = _timeProvider.GetUtcNow();
            RemoveExpired(now.Subtract(requestWindow));
            (int Sender, int Room, int Recipient) key =
                (invitation.Sender.UserId, invitation.RoomId, invitation.Recipient.UserId);
            DateTimeOffset retryAt = _recent.GetValueOrDefault(key).AddSeconds(ResendDelaySeconds);
            if (_requests.TryGetValue(invitation.Sender.UserId, out Queue<DateTimeOffset>? history)
                && history.Count >= MaximumRequestsPerHour)
            {
                DateTimeOffset windowEnd = history.Peek().Add(requestWindow);
                retryAt = retryAt > windowEnd ? retryAt : windowEnd;
            }
            if (_sending.Contains(invitation.InvitationId)
                || history is null && _requests.Count >= MaximumTrackedSenders)
            {
                DateTimeOffset nextAttempt = now.AddSeconds(ResendDelaySeconds);
                retryAt = retryAt > nextAttempt ? retryAt : nextAttempt;
            }
            if (retryAt > now)
            {
                return (int)Math.Ceiling((retryAt - now).TotalSeconds);
            }
            history ??= new Queue<DateTimeOffset>();
            history.Enqueue(now);
            _requests[invitation.Sender.UserId] = history;
            _recent[key] = now;
            _sending.Add(invitation.InvitationId);
            return 0;
        }
    }

    public void Complete(RoomInvitationEntry invitation)
    {
        ArgumentNullException.ThrowIfNull(invitation);
        lock (_gate)
        {
            _sending.Remove(invitation.InvitationId);
        }
    }

    private void RemoveExpired(DateTimeOffset cutoff)
    {
        foreach (int sender in _requests.Keys.ToArray())
        {
            Queue<DateTimeOffset> history = _requests[sender];
            while (history.TryPeek(out DateTimeOffset time) && time <= cutoff)
            {
                history.Dequeue();
            }
            if (history.Count == 0)
            {
                _requests.Remove(sender);
            }
        }
        foreach ((int Sender, int Room, int Recipient) key in _recent.Keys.ToArray())
        {
            if (_recent[key] <= cutoff)
            {
                _recent.Remove(key);
            }
        }
    }
}
