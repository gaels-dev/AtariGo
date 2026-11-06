using System.Collections.Generic;
using System.Threading;

using AtariGo.Contracts;

namespace AtariGo.Client.Services;

public interface IGameSessionClient
{
    IAsyncEnumerable<GameSessionServerEvent> ConnectAsync(
        long gameId,
        CancellationToken cancellationToken);
}
