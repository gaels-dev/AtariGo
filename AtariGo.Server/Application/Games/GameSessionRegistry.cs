using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Channels;

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

                GameSessionConnection connection =
                    state.CreateConnection(player.PlayerKey);
                state.ReplaceConnection(connection);
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

            if (state.Connections.Count == 0)
            {
                _sessions.TryRemove(connection.GameId, out _);
            }
            else
            {
                PublishSnapshots(state);
            }
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

    private static void PublishSnapshots(GameSessionState state)
    {
        foreach (KeyValuePair<string, GameSessionConnection> entry
            in state.Connections)
        {
            GameSessionSnapshot snapshot = state.CreateSnapshot(entry.Key);
            GameSessionDelivery delivery = new GameSessionDelivery
            {
                RecipientPlayerKey = entry.Key,
                Snapshot = snapshot
            };
            state.Publish(entry.Value, delivery);
        }
    }
}

internal sealed class GameSessionState
{
    private const string EmptyStone = "Empty";

    public GameSessionState(GameSessionDefinition definition)
    {
        Definition = definition;
    }

    public object SyncRoot { get; } = new object();

    public GameSessionDefinition Definition { get; }

    public Dictionary<string, GameSessionConnection> Connections { get; } =
        new Dictionary<string, GameSessionConnection>(StringComparer.Ordinal);

    public HashSet<string> ConnectedPlayerHistory { get; } =
        new HashSet<string>(StringComparer.Ordinal);

    public GameSessionParticipant? FindParticipant(string playerKey)
    {
        if (Definition.Player.PlayerKey == playerKey)
        {
            return Definition.Player;
        }

        if (Definition.Opponent.PlayerKey == playerKey)
        {
            return Definition.Opponent;
        }

        return null;
    }

    public GameSessionConnection CreateConnection(string playerKey)
    {
        Channel<GameSessionDelivery> events = Channel.CreateUnbounded<GameSessionDelivery>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });
        return new GameSessionConnection(Definition.GameId, playerKey, events);
    }

    public void ReplaceConnection(GameSessionConnection connection)
    {
        if (Connections.TryGetValue(connection.PlayerKey,
            out GameSessionConnection? previousConnection))
        {
            Complete(previousConnection);
        }

        Connections[connection.PlayerKey] = connection;
        ConnectedPlayerHistory.Add(connection.PlayerKey);
    }

    public bool RemoveConnection(GameSessionConnection connection)
    {
        if (!Connections.TryGetValue(connection.PlayerKey,
            out GameSessionConnection? currentConnection)
            || !ReferenceEquals(currentConnection, connection))
        {
            return false;
        }

        Connections.Remove(connection.PlayerKey);
        Complete(connection);
        return true;
    }

    public GameSessionSnapshot CreateSnapshot(string playerKey)
    {
        GameSessionParticipant player = FindParticipant(playerKey)
            ?? throw new InvalidOperationException(
                "The player does not belong to the game session.");
        GameSessionParticipant opponent = player.PlayerKey == Definition.Player.PlayerKey
            ? Definition.Opponent
            : Definition.Player;
        bool opponentConnected = Connections.ContainsKey(opponent.PlayerKey);
        bool opponentDisconnected = !opponentConnected
            && ConnectedPlayerHistory.Contains(opponent.PlayerKey);
        return new GameSessionSnapshot
        {
            GameId = Definition.GameId,
            BoardSize = GameSessionRules.BoardSize,
            CaptureGoal = Definition.CaptureGoal,
            GameTimeSeconds = Definition.GameTimeSeconds,
            TurnTimeSeconds = Definition.TurnTimeSeconds,
            Player = player,
            Opponent = opponent,
            OpponentConnected = opponentConnected,
            OpponentDisconnected = opponentDisconnected,
            MaximumChatMessageLength = GameSessionRules.MaximumChatMessageLength,
            ChatHistory = ChatHistory.ToArray(),
            Cells = CreateEmptyBoard()
        };
    }

    public List<GameSessionChatMessage> ChatHistory { get; } =
        new List<GameSessionChatMessage>();

    public GameSessionChatMessage CreateChatMessage(
        string senderPlayerKey,
        string text,
        long sentAtUnixMilliseconds)
    {
        GameSessionParticipant sender = FindParticipant(senderPlayerKey)
            ?? throw new InvalidOperationException(
                "The sender does not belong to the game session.");
        return new GameSessionChatMessage
        {
            SenderPlayerKey = senderPlayerKey,
            SenderName = sender.DisplayName,
            Text = text,
            SentAtUnixMilliseconds = sentAtUnixMilliseconds
        };
    }

    public void AddChatMessage(GameSessionChatMessage chatMessage)
    {
        ChatHistory.Add(chatMessage);
        if (ChatHistory.Count > GameSessionRules.MaximumChatHistoryCount)
        {
            ChatHistory.RemoveAt(0);
        }
    }

    public void Publish(
        GameSessionConnection connection,
        GameSessionDelivery delivery)
    {
        if (Connections.TryGetValue(connection.PlayerKey,
            out GameSessionConnection? currentConnection)
            && ReferenceEquals(connection, currentConnection))
        {
            connection.EventWriter.TryWrite(delivery);
        }
    }

    private static IReadOnlyList<GameSessionBoardCell> CreateEmptyBoard()
    {
        List<GameSessionBoardCell> cells = new List<GameSessionBoardCell>(
            GameSessionRules.BoardSize * GameSessionRules.BoardSize);
        for (int row = 0; row < GameSessionRules.BoardSize; row++)
        {
            for (int column = 0; column < GameSessionRules.BoardSize; column++)
            {
                cells.Add(new GameSessionBoardCell(row, column, EmptyStone));
            }
        }

        return cells;
    }

    private static void Complete(GameSessionConnection connection)
    {
        connection.EventWriter.TryComplete();
    }
}
