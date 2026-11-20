using AtariGo.Client.Models;

namespace AtariGo.Client.Services;

public interface IGameHistoryClient
{
    Task<GameHistoryQueryResult> ListGamesByPlayerAsync(CancellationToken cancellationToken);
}
