namespace AtariGo.Server.Application.Matchmaking;

public sealed class PublicMatchState
{
    internal PublicMatchState(
        Guid matchId,
        MatchmakingTicket blackTicket,
        MatchmakingTicket whiteTicket,
        DateTimeOffset countdownEndsAt)
    {
        MatchId = matchId;
        BlackTicket = blackTicket;
        WhiteTicket = whiteTicket;
        CountdownEndsAt = countdownEndsAt;
        CountdownCancellation = new CancellationTokenSource();
        GameCompletion = new TaskCompletionSource<long>(
            TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public Guid MatchId { get; }

    public MatchmakingTicket BlackTicket { get; }

    public MatchmakingTicket WhiteTicket { get; }

    public DateTimeOffset CountdownEndsAt { get; }

    public CancellationTokenSource CountdownCancellation { get; }

    public TaskCompletionSource<long> GameCompletion { get; }

    public bool IsStarting { get; set; }

    public bool IsCompleted { get; set; }
}
