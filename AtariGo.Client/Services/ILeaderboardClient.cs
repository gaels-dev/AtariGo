using System.Threading;
using System.Threading.Tasks;
using AtariGo.Contracts;

namespace AtariGo.Client.Services;

public interface ILeaderboardClient
{
    Task<GetTopPlayersResponse> GetTopPlayersAsync(
        int limit,
        CancellationToken cancellationToken = default);
}
