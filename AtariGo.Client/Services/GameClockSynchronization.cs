
namespace AtariGo.Client.Services;

public sealed class GameClockSynchronization
{
    private const int RoundTripLegs = 2;
    private readonly TimeProvider _timeProvider;
    private long _connectionStartedTimestamp;
    private long _anchorTimestamp;
    private double _serverAnchorMilliseconds;

    public GameClockSynchronization(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
    }

    public bool IsSynchronized { get; private set; }

    public void BeginConnection()
    {
        IsSynchronized = false;
        _connectionStartedTimestamp = _timeProvider.GetTimestamp();
    }

    public void ObserveServerTime(long serverTimeMilliseconds)
    {
        if (serverTimeMilliseconds <= 0)
        {
            return;
        }
        long received = _timeProvider.GetTimestamp();
        double estimatedNow = serverTimeMilliseconds;
        if (IsSynchronized)
        {
            estimatedNow = Math.Max(estimatedNow, EstimateServerTime(received));
        }
        else
        {
            // The initial handshake estimates transit; later snapshots cannot turn the clock back.
            TimeSpan roundTrip = _timeProvider.GetElapsedTime(
                _connectionStartedTimestamp, received);
            estimatedNow += roundTrip.TotalMilliseconds / RoundTripLegs;
        }
        _serverAnchorMilliseconds = estimatedNow;
        _anchorTimestamp = received;
        IsSynchronized = true;
    }

    public double GetRemainingMilliseconds(long deadlineMilliseconds)
    {
        if (!IsSynchronized)
        {
            throw new InvalidOperationException("The game clock has not been synchronized.");
        }
        return Math.Max(0, deadlineMilliseconds - EstimateServerTime(_timeProvider.GetTimestamp()));
    }

    private double EstimateServerTime(long timestamp)
    {
        return _serverAnchorMilliseconds
            + _timeProvider.GetElapsedTime(_anchorTimestamp, timestamp).TotalMilliseconds;
    }
}
