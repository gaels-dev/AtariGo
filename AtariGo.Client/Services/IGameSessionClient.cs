

namespace AtariGo.Client.Services;

public interface IGameSessionClient
{
    Task<IGameSessionStream> ConnectAsync(
        long gameId,
        CancellationToken cancellationToken);
}
