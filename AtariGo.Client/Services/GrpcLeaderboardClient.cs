using System.Threading;
using System.Threading.Tasks;
using AtariGo.Contracts;
using Grpc.Net.Client;

namespace AtariGo.Client.Services;

public sealed class GrpcLeaderboardClient : ILeaderboardClient
{
    private readonly GrpcChannel _channel = GrpcChannel.ForAddress("http://localhost:5026");
    private Leaderboard.LeaderboardClient? _client;

    public Task<GetTopPlayersResponse> GetTopPlayersAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        _client ??= new Leaderboard.LeaderboardClient(_channel);

        return _client.GetTopPlayersAsync(
            new GetTopPlayersRequest { Limit = limit },
            cancellationToken: cancellationToken).ResponseAsync;
    }
}
