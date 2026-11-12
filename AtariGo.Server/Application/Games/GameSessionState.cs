using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Channels;

using AtariGo.GameLogic.Models;

namespace AtariGo.Server.Application.Games;

internal sealed class GameSessionState
{
    private const string EmptyStone = "Empty";

    public GameSessionState(GameSessionDefinition definition)
    {
        Definition = definition;
        Progress = definition.Progress;
        Version = definition.Version;
    }

    public GameRuntimeState? Progress { get; set; }

    public long Version { get; set; }

    public DateTime LastActivityUtc { get; set; }

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
        Channel<GameSessionDelivery> events = Channel.CreateBounded<GameSessionDelivery>(
            new BoundedChannelOptions(GameSessionRules.MaximumPendingEvents)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait
            });
        return new GameSessionConnection(Definition.GameId, playerKey, events);
    }

    public void ReplaceConnection(GameSessionConnection connection)
    {
        if (Connections.TryGetValue(connection.PlayerKey,
            out GameSessionConnection? previousConnection))
        {
            previousConnection.IsReplaced = true;
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

    public GameSessionSnapshot CreateSnapshot(string playerKey, DateTime now)
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
            ServerTimeUtc = now,
            Version = Version,
            Progress = Progress,
            Cells = CreateBoard()
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
            if (!connection.EventWriter.TryWrite(delivery))
            {
                connection.EventWriter.TryComplete(new IOException("The game client is too slow."));
            }
        }
    }

    private IReadOnlyList<GameSessionBoardCell> CreateBoard()
    {
        List<GameSessionBoardCell> cells = new List<GameSessionBoardCell>(
            GameSessionRules.BoardSize * GameSessionRules.BoardSize);
        for (int row = 0; row < GameSessionRules.BoardSize; row++)
        {
            for (int column = 0; column < GameSessionRules.BoardSize; column++)
            {
                int index = row * GameSessionRules.BoardSize + column;
                string stone = Progress is null
                    ? EmptyStone : Progress.Board.Board[index].ToString();
                cells.Add(new GameSessionBoardCell(row, column, stone));
            }
        }

        return cells;
    }

    private static void Complete(GameSessionConnection connection)
    {
        connection.EventWriter.TryComplete();
    }
}
