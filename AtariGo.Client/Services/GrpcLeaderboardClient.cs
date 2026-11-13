
using Grpc.Core;

using AtariGo.Contracts;

namespace AtariGo.Client.Services
{
    public sealed class GrpcLeaderboardClient : ILeaderboardClient
    {
        private readonly Leaderboard.LeaderboardClient _client;

        public GrpcLeaderboardClient()
            : this(GrpcClientConnection.Shared.CallInvoker)
        {
        }

        public GrpcLeaderboardClient(CallInvoker callInvoker)
        {
            ArgumentNullException.ThrowIfNull(callInvoker);
            _client = new Leaderboard.LeaderboardClient(callInvoker);
        }

        public Task<GetTopPlayersResponse> GetTopPlayersAsync(
            int limit,
            CancellationToken cancellationToken = default)
        {
            return _client.GetTopPlayersAsync(
                new GetTopPlayersRequest { Limit = limit },
                GrpcCallPolicy.CreateOptions(cancellationToken)).ResponseAsync;
        }
    }
}
