using Grpc.Core;

using AtariGo.Contracts;
using AtariGo.Diagnostics;

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

        public async Task<GetTopPlayersResponse> GetTopPlayersAsync(
            int limit,
            CancellationToken cancellationToken = default)
        {
            GetTopPlayersResponse response = await ClientPlayerDiagnostics.ExecuteAsync(() =>
                _client.GetTopPlayersAsync(
                new GetTopPlayersRequest { Limit = limit },
                GrpcCallPolicy.CreateOptions(cancellationToken)).ResponseAsync,
                PlayerOperation.QueryLeaderboard, cancellationToken);
            ClientPlayerDiagnostics.Record(PlayerOperation.QueryLeaderboard,
                new DiagnosticEventDetails { Outcome = DiagnosticOutcome.Succeeded });
            return response;
        }
    }
}
