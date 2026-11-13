namespace AtariGo.Server.Application.Leaderboard;

public interface ILeaderboardService
{
    /// <summary>Returns the highest ranked active players by completed wins.</summary>
    /// <param name="requestedLimit">The requested number of entries.</param>
    /// <param name="cancellationToken">Cancels the database query.</param>
    /// <returns>The ordered player entries.</returns>
    Task<IReadOnlyList<PlayerLeaderboardEntry>> GetTopPlayersAsync(
        int requestedLimit,
        CancellationToken cancellationToken);
}
