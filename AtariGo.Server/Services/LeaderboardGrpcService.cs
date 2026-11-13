using Grpc.Core;

using AtariGo.Contracts;
using AtariGo.Server.Application.Leaderboard;

namespace AtariGo.Server.Services;

public sealed class LeaderboardGrpcService : Leaderboard.LeaderboardBase
{
    private readonly ILeaderboardService _leaderboardService;

    public LeaderboardGrpcService(ILeaderboardService leaderboardService)
    {
        ArgumentNullException.ThrowIfNull(leaderboardService);
        _leaderboardService = leaderboardService;
    }

    private static GetTopPlayersResponse CreateResponse(
        IReadOnlyList<PlayerLeaderboardEntry> players)
    {
        var response = new GetTopPlayersResponse();
        for (int index = 0; index < players.Count; index++)
        {
            response.Entries.Add(new LeaderboardEntry
            {
                Position = index + 1,
                UserName = players[index].UserName,
                Wins = players[index].Wins
            });
        }

        return response;
    }

    public override async Task<GetTopPlayersResponse> GetTopPlayers(
        GetTopPlayersRequest request,
        ServerCallContext context)
    {
        IReadOnlyList<PlayerLeaderboardEntry> players =
            await _leaderboardService.GetTopPlayersAsync(
                request.Limit,
                context.CancellationToken);

        return CreateResponse(players);
    }
}
