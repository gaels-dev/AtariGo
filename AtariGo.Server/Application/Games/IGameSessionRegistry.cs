namespace AtariGo.Server.Application.Games;

public interface IGameSessionRegistry
{
    GameSessionConnection Connect(GameSessionDefinition definition);

    ChatMessageSubmissionResult PublishChatMessage(
        GameSessionConnection connection,
        string text);

    ChatMessageSubmissionResult RejectChatMessage(
        GameSessionConnection connection,
        ChatMessageRejectionReason reason);

    bool IsCurrent(GameSessionConnection connection);

    bool AreBothConnected(long gameId);
    GameConnectionPresence GetPresence(long gameId);
    GameRuntimeState? GetProgress(long gameId, long version);

    void PublishProgress(long gameId, GameRuntimeState progress, long version);

    void PublishActionResult(GameSessionConnection connection, GameActionResult result);

    void PruneIdleSessions();

    void Disconnect(GameSessionConnection connection);

    void DisconnectUser(int userId);
}
