namespace AtariGo.Server.Application.Games;

public interface IGameSessionRegistry
{
    GameSessionConnection Connect(GameSessionDefinition definition);

    void Disconnect(GameSessionConnection connection);
}
