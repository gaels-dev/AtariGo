using System.Threading.Channels;

namespace AtariGo.Server.Application.Games;

public sealed class GameSessionConnection : IDisposable
{
    private readonly CancellationTokenSource _revocation = new CancellationTokenSource();

    internal GameSessionConnection(
        long gameId,
        GameSessionParticipant player,
        Channel<GameSessionDelivery> events)
    {
        GameId = gameId;
        PlayerKey = player.PlayerKey;
        StoneColor = player.StoneColor;
        Events = events.Reader;
        EventWriter = events.Writer;
    }

    public bool IsReplaced
    {
        get; internal set;
    }

    public bool IsRevoked
    {
        get; internal set;
    }

    public bool IsGracefulClosure { get; internal set; }

    public CancellationToken RevocationToken => _revocation.Token;

    public long GameId
    {
        get;
    }

    public string PlayerKey
    {
        get;
    }

    public string StoneColor { get; }

    public ChannelReader<GameSessionDelivery> Events
    {
        get;
    }

    internal ChannelWriter<GameSessionDelivery> EventWriter
    {
        get;
    }

    public void Dispose()
    {
        _revocation.Dispose();
    }

    internal void Revoke()
    {
        IsRevoked = true;
        _revocation.Cancel();
    }

    internal void CompleteOutput()
    {
        EventWriter.TryComplete();
    }
}
