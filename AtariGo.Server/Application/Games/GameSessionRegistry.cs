using System.Collections.Concurrent;

namespace AtariGo.Server.Application.Games;

public sealed class GameSessionRegistry : IGameSessionRegistry
{
    private readonly ConcurrentDictionary<long, GameSessionState> _sessions = new();
    private readonly TimeProvider _timeProvider;

    public GameSessionRegistry(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
    }

    public GameSessionConnection Connect(GameSessionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        while (true)
        {
            GameSessionState state = _sessions.GetOrAdd(
                definition.GameId,
                CreateState,
                definition);

            lock (state.SyncRoot)
            {
                if (!IsCurrentState(definition.GameId, state))
                {
                    continue;
                }

                GameSessionParticipant? player = state.FindParticipant(
                    definition.Player.PlayerKey);
                if (player is null)
                {
                    throw new InvalidOperationException(
                        "The player does not belong to the game session.");
                }

                if (definition.Progress is not null && definition.Version >= state.Version)
                {
                    state.Progress = definition.Progress;
                    state.Version = definition.Version;
                }
                GameSessionConnection connection =
                    state.CreateConnection(player.PlayerKey);
                state.ReplaceConnection(connection);
                state.LastActivityUtc = _timeProvider.GetUtcNow().UtcDateTime;
                PublishSnapshots(state);
                return connection;
            }
        }
    }

    public void Disconnect(GameSessionConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (!_sessions.TryGetValue(connection.GameId, out GameSessionState? state))
        {
            return;
        }

        lock (state.SyncRoot)
        {
            if (!state.RemoveConnection(connection))
            {
                return;
            }

            state.LastActivityUtc = _timeProvider.GetUtcNow().UtcDateTime;
            PublishSnapshots(state);
        }
    }

    public ChatMessageSubmissionResult PublishChatMessage(
        GameSessionConnection connection,
        string text)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (string.IsNullOrWhiteSpace(text))
        {
            return PublishChatRejection(
                connection,
                ChatMessageRejectionReason.Empty,
                ChatMessageSubmissionResult.Empty);
        }

        string normalizedText = text.Trim();
        if (normalizedText.Length > GameSessionRules.MaximumChatMessageLength)
        {
            return PublishChatRejection(
                connection,
                ChatMessageRejectionReason.TooLong,
                ChatMessageSubmissionResult.TooLong);
        }

        if (!_sessions.TryGetValue(connection.GameId, out GameSessionState? state))
        {
            return ChatMessageSubmissionResult.NotConnected;
        }

        lock (state.SyncRoot)
        {
            if (!IsCurrentConnection(state, connection))
            {
                return ChatMessageSubmissionResult.NotConnected;
            }

            GameSessionChatMessage chatMessage = state.CreateChatMessage(
                connection.PlayerKey,
                normalizedText,
                _timeProvider.GetUtcNow().ToUnixTimeMilliseconds());
            state.AddChatMessage(chatMessage);
            PublishChatMessage(state, chatMessage);
            return ChatMessageSubmissionResult.Accepted;
        }
    }

    public bool IsCurrent(GameSessionConnection connection)
    {
        if (!_sessions.TryGetValue(connection.GameId, out GameSessionState? state))
        {
            return false;
        }
        lock (state.SyncRoot)
        {
            return IsCurrentConnection(state, connection);
        }
    }

    public bool AreBothConnected(long gameId)
    {
        if (!_sessions.TryGetValue(gameId, out GameSessionState? state))
        {
            return false;
        }
        lock (state.SyncRoot)
        {
            return state.Connections.Count == GameSessionRules.PlayerCount;
        }
    }

    public void PublishProgress(long gameId, GameRuntimeState progress, long version)
    {
        if (!_sessions.TryGetValue(gameId, out GameSessionState? state))
        {
            return;
        }
        lock (state.SyncRoot)
        {
            if (version >= state.Version)
            {
                state.Progress = progress;
                state.Version = version;
                PublishSnapshots(state);
            }
        }
    }

    public void PublishActionResult(GameSessionConnection connection, GameActionResult result)
    {
        if (!_sessions.TryGetValue(connection.GameId, out GameSessionState? state))
        {
            return;
        }
        lock (state.SyncRoot)
        {
            state.Publish(connection, new GameSessionDelivery { ActionResult = result });
        }
    }

    public void PruneIdleSessions()
    {
        TimeSpan retention = TimeSpan.FromHours(1);
        DateTime cutoff = _timeProvider.GetUtcNow().UtcDateTime - retention;
        foreach (KeyValuePair<long, GameSessionState> entry in _sessions)
        {
            lock (entry.Value.SyncRoot)
            {
                if (entry.Value.Connections.Count == 0 && entry.Value.LastActivityUtc < cutoff)
                {
                    _sessions.TryRemove(entry);
                }
            }
        }
    }

    private ChatMessageSubmissionResult PublishChatRejection(
        GameSessionConnection connection,
        ChatMessageRejectionReason reason,
        ChatMessageSubmissionResult result)
    {
        if (!_sessions.TryGetValue(connection.GameId, out GameSessionState? state))
        {
            return ChatMessageSubmissionResult.NotConnected;
        }

        lock (state.SyncRoot)
        {
            if (!IsCurrentConnection(state, connection))
            {
                return ChatMessageSubmissionResult.NotConnected;
            }

            GameSessionDelivery delivery = new GameSessionDelivery
            {
                RecipientPlayerKey = connection.PlayerKey,
                RejectionReason = reason
            };
            state.Publish(connection, delivery);
            return result;
        }
    }

    private static void PublishChatMessage(
        GameSessionState state,
        GameSessionChatMessage chatMessage)
    {
        foreach (KeyValuePair<string, GameSessionConnection> entry
            in state.Connections)
        {
            GameSessionDelivery delivery = new GameSessionDelivery
            {
                RecipientPlayerKey = entry.Key,
                ChatMessage = chatMessage
            };
            state.Publish(entry.Value, delivery);
        }
    }

    private static bool IsCurrentConnection(
        GameSessionState state,
        GameSessionConnection connection)
    {
        return state.Connections.TryGetValue(
                connection.PlayerKey,
                out GameSessionConnection? currentConnection)
            && ReferenceEquals(currentConnection, connection);
    }

    private static GameSessionState CreateState(
        long _,
        GameSessionDefinition definition)
    {
        return new GameSessionState(definition);
    }

    private bool IsCurrentState(long gameId, GameSessionState state)
    {
        return _sessions.TryGetValue(gameId, out GameSessionState? currentState)
            && ReferenceEquals(state, currentState);
    }

    private void PublishSnapshots(GameSessionState state)
    {
        foreach (KeyValuePair<string, GameSessionConnection> entry
            in state.Connections)
        {
            GameSessionSnapshot snapshot = state.CreateSnapshot(
                entry.Key, _timeProvider.GetUtcNow().UtcDateTime);
            GameSessionDelivery delivery = new GameSessionDelivery
            {
                RecipientPlayerKey = entry.Key,
                Snapshot = snapshot
            };
            state.Publish(entry.Value, delivery);
        }
    }
}
