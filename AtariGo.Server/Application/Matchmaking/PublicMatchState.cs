namespace AtariGo.Server.Application.Matchmaking;

public sealed class PublicMatchState
{
    internal PublicMatchState(
        MatchmakingTicket blackTicket,
        MatchmakingTicket whiteTicket,
        DateTimeOffset countdownEndsAt)
    {
        ArgumentNullException.ThrowIfNull(blackTicket);
        ArgumentNullException.ThrowIfNull(whiteTicket);
        MatchId = Guid.NewGuid();
        BlackTicket = blackTicket;
        WhiteTicket = whiteTicket;
        CountdownEndsAt = countdownEndsAt;
        CountdownCancellation = new CancellationTokenSource();
        GameCompletion = new TaskCompletionSource<long>(
            TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public Guid MatchId
    {
        get;
    }

    public MatchmakingTicket BlackTicket
    {
        get;
    }

    public MatchmakingTicket WhiteTicket
    {
        get;
    }

    public DateTimeOffset CountdownEndsAt
    {
        get;
    }

    public CancellationTokenSource CountdownCancellation
    {
        get;
    }

    public TaskCompletionSource<long> GameCompletion
    {
        get;
    }

    public bool IsStarting
    {
        get; set;
    }

    public bool IsCompleted
    {
        get; set;
    }
}
