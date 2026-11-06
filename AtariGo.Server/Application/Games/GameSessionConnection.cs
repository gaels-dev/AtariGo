using System.Threading.Channels;

namespace AtariGo.Server.Application.Games;

public sealed class GameSessionConnection
{
    internal GameSessionConnection(
        long gameId,
        string playerKey,
        Channel<GameSessionDelivery> events)
    {
        GameId = gameId;
        PlayerKey = playerKey;
        Events = events.Reader;
        EventWriter = events.Writer;
    }

    public long GameId { get; }

    public string PlayerKey { get; }

    public ChannelReader<GameSessionDelivery> Events { get; }

    internal ChannelWriter<GameSessionDelivery> EventWriter { get; }
}
