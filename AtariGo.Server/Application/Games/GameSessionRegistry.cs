using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace AtariGo.Server.Application.Games;

public sealed class GameSessionRegistry : IGameSessionRegistry
{
    private readonly ConcurrentDictionary<long, GameSessionState> _sessions = new();

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
            state.Publish(entry.Value, snapshot);
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
        Channel<GameSessionSnapshot> snapshots = Channel.CreateUnbounded<GameSessionSnapshot>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });
        return new GameSessionConnection(Definition.GameId, playerKey, snapshots);
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
            Cells = CreateEmptyBoard()
        };
    }

    public void Publish(
        GameSessionConnection connection,
        GameSessionSnapshot snapshot)
    {
        if (Connections.TryGetValue(connection.PlayerKey,
            out GameSessionConnection? currentConnection)
            && ReferenceEquals(connection, currentConnection))
        {
            connection.SnapshotWriter.TryWrite(snapshot);
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
        connection.SnapshotWriter.TryComplete();
    }
}
