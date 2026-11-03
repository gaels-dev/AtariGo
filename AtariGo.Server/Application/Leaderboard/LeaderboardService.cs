using Microsoft.EntityFrameworkCore;

using AtariGo.Server.Data;

namespace AtariGo.Server.Application.Leaderboard;

public sealed class LeaderboardService : ILeaderboardService
{
    private const int DefaultMaximumEntries = 10;
    private readonly AtariGoDbContext _dbContext;

    public LeaderboardService(AtariGoDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<PlayerLeaderboardEntry>> GetTopPlayersAsync(
        int requestedLimit,
        CancellationToken cancellationToken)
    {
        int limit = NormalizeLimit(requestedLimit);

        var players = await _dbContext.GamePlayers
            .AsNoTracking()
            .Where(player =>
                player.UserId != null
                && player.Result == DatabaseValues.GamePlayerResults.Won
                && player.Game.State == DatabaseValues.GameStates.Completed
                && player.User!.State == DatabaseValues.AccountStates.Active)
            .GroupBy(player => new
            {
                player.UserId,
                player.User!.UserName
            })
            .Select(group => new
            {
                UserName = group.Key.UserName,
                Wins = group.Count()
            })
            .OrderByDescending(player => player.Wins)
            .ThenBy(player => player.UserName)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return players
            .Select(player => new PlayerLeaderboardEntry(
                player.UserName,
                player.Wins))
            .ToList();
    }

    private static int NormalizeLimit(int requestedLimit)
    {
        if (requestedLimit <= 0)
        {
            return DefaultMaximumEntries;
        }

        return Math.Min(requestedLimit, DefaultMaximumEntries);
    }
}
