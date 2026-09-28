using AtariGo.Contracts;
using AtariGo.Server.Models;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;

namespace AtariGo.Server.Services;

public sealed class LeaderboardGrpcService(AtariGoDbContext dbContext)
    : Leaderboard.LeaderboardBase
{
    private const int MaximumEntries = 10;

    public override async Task<GetTopPlayersResponse> GetTopPlayers(
        GetTopPlayersRequest request,
        ServerCallContext context)
    {
        int limit = request.Limit <= 0
            ? MaximumEntries
            : Math.Min(request.Limit, MaximumEntries);

        var players = await dbContext.GamePlayers
            .AsNoTracking()
            .Where(player =>
                player.UserId != null
                && player.Result == "Won"
                && player.Game.State == "Completed"
                && player.User!.State == "Active")
            .GroupBy(player => new
            {
                player.UserId,
                player.User!.UserName
            })
            .Select(group => new
            {
                group.Key.UserName,
                Wins = group.Count()
            })
            .OrderByDescending(player => player.Wins)
            .ThenBy(player => player.UserName)
            .Take(limit)
            .ToListAsync(context.CancellationToken);

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
}
