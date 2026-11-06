using System.Threading.Channels;

namespace AtariGo.Server.Application.Games;

public sealed class GameSessionConnection
{
    internal GameSessionConnection(
        long gameId,
        string playerKey,
        Channel<GameSessionSnapshot> snapshots)
    {
        GameId = gameId;
        PlayerKey = playerKey;
        Snapshots = snapshots.Reader;
        SnapshotWriter = snapshots.Writer;
    }

    public long GameId { get; }

    public string PlayerKey { get; }

    public ChannelReader<GameSessionSnapshot> Snapshots { get; }

    internal ChannelWriter<GameSessionSnapshot> SnapshotWriter { get; }
}
