using AtariGo.Server.Application.Sessions;

namespace AtariGo.Server.Application.GameHistory;

public interface IGameHistoryService
{
    Task<IReadOnlyList<GameHistoryEntry>> ListGamesByPlayerAsync(
        SessionIdentity identity,
        CancellationToken cancellationToken);
}
