namespace AtariGo.Server.Application.Matchmaking;

public interface IPublicMatchmakingQueue
{
    MatchmakingTicket? Join(MatchmakingPlayer player);

    void Disconnect(MatchmakingTicket ticket);

    bool TryClaimStart(PublicMatchState match);

    void CompleteStart(PublicMatchState match, long gameId);

    void FailStart(PublicMatchState match);
}
