namespace AtariGo.Server.Application.Games;

public interface IGameSessionCatalog
{
    Task<GameSessionDefinition?> FindForPlayerAsync(
        GameSessionPlayerRequest request,
        CancellationToken cancellationToken);
}
