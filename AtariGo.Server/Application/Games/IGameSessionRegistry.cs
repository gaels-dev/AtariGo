namespace AtariGo.Server.Application.Games;

public interface IGameSessionRegistry
{
    GameSessionConnection Connect(GameSessionDefinition definition);

    ChatMessageSubmissionResult PublishChatMessage(
        GameSessionConnection connection,
        string text);

    bool IsCurrent(GameSessionConnection connection);

    bool AreBothConnected(long gameId);

    void PublishProgress(long gameId, GameRuntimeState progress, long version);

    void PublishActionResult(GameSessionConnection connection, GameActionResult result);

    void PruneIdleSessions();

    void Disconnect(GameSessionConnection connection);
}
