using AtariGo.Contracts;

namespace AtariGo.Client.Services;

public interface IGameSessionStream : IAsyncDisposable
{
    Task CompleteAsync(CancellationToken cancellationToken);

    IAsyncEnumerable<GameSessionServerEvent> ReadEventsAsync(
        CancellationToken cancellationToken);

    Task SendEventAsync(GameSessionClientEvent clientEvent, CancellationToken cancellationToken);

    Task SendChatMessageAsync(
        string text,
        CancellationToken cancellationToken);
}
