using AtariGo.Contracts;

namespace AtariGo.Client.Services;

public interface IMatchmakingClient
{
    IAsyncEnumerable<MatchmakingEvent> SearchPublicMatchAsync(
        CancellationToken cancellationToken);
}
