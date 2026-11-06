using System.Threading.Channels;

using AtariGo.Contracts;

namespace AtariGo.Server.Application.Matchmaking;

public sealed class MatchmakingTicket
{
    internal MatchmakingTicket(MatchmakingPlayer player)
    {
        Player = player;
        Notifications = Channel.CreateUnbounded<MatchmakingEvent>();
    }

    public MatchmakingPlayer Player { get; }

    public ChannelReader<MatchmakingEvent> Events => Notifications.Reader;

    internal Channel<MatchmakingEvent> Notifications { get; }

    internal LinkedListNode<MatchmakingTicket>? QueueNode { get; set; }

    internal PublicMatchState? Match { get; set; }
}
