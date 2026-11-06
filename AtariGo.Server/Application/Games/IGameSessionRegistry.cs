namespace AtariGo.Server.Application.Games;

public interface IGameSessionRegistry
{
    GameSessionConnection Connect(GameSessionDefinition definition);

    ChatMessageSubmissionResult PublishChatMessage(
        GameSessionConnection connection,
        string text);

    void Disconnect(GameSessionConnection connection);
}
