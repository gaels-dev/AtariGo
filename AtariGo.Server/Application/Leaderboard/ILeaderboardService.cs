namespace AtariGo.Server.Application.Leaderboard;

public interface ILeaderboardService
{
    Task<IReadOnlyList<PlayerLeaderboardEntry>> GetTopPlayersAsync(
        int requestedLimit,
        CancellationToken cancellationToken);
}
