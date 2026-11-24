using Microsoft.Extensions.Logging.Abstractions;

using Grpc.Core;

using AtariGo.Contracts;
using AtariGo.Diagnostics;
using AtariGo.Server.Diagnostics;
using AtariGo.Server.Application.Leaderboard;

namespace AtariGo.Server.Services;

public sealed class LeaderboardGrpcService : Leaderboard.LeaderboardBase
{
    private readonly ILeaderboardService _leaderboardService;
    private readonly ILogger _logger;

    public LeaderboardGrpcService(ILeaderboardService leaderboardService)
        : this(leaderboardService, NullLogger<LeaderboardGrpcService>.Instance)
    {
    }

    public LeaderboardGrpcService(ILeaderboardService leaderboardService,
        ILogger<LeaderboardGrpcService> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
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
                Wins = players[index].Wins,
                AvatarId = players[index].AvatarId
            });
        }

        return response;
    }

    public override async Task<GetTopPlayersResponse> GetTopPlayers(
        GetTopPlayersRequest request,
        ServerCallContext context)
    {
        try
        {
            IReadOnlyList<PlayerLeaderboardEntry> players =
                await _leaderboardService.GetTopPlayersAsync(
                    request.Limit,
                    context.CancellationToken);
            GetTopPlayersResponse response = CreateResponse(players);
            ServerPlayerDiagnostics.Record(_logger, PlayerOperation.QueryLeaderboard,
                new DiagnosticEventDetails { Outcome = DiagnosticOutcome.Succeeded });
            return response;
        }
        catch (Exception exception)
        {
            ServerPlayerDiagnostics.RecordFailure(_logger, PlayerOperation.QueryLeaderboard,
                (exception, context.CancellationToken));
            throw;
        }
    }
}
