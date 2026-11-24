using AtariGo.Server.Application.Leaderboard;

namespace AtariGo.Server.Tests.Diagnostics;

internal sealed class ThrowingLeaderboardService : ILeaderboardService
{
    internal required Exception Failure
    {
        get; init;
    }

    public Task<IReadOnlyList<PlayerLeaderboardEntry>> GetTopPlayersAsync(int requestedLimit,
        CancellationToken cancellationToken)
    {
        return Task.FromException<IReadOnlyList<PlayerLeaderboardEntry>>(Failure);
    }
}
