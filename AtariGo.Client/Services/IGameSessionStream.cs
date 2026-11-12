using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using AtariGo.Contracts;

namespace AtariGo.Client.Services;

public interface IGameSessionStream : IAsyncDisposable
{
    IAsyncEnumerable<GameSessionServerEvent> ReadEventsAsync(
        CancellationToken cancellationToken);

    Task SendEventAsync(GameSessionClientEvent clientEvent, CancellationToken cancellationToken);

    Task SendChatMessageAsync(
        string text,
        CancellationToken cancellationToken);
}
