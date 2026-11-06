using System.Threading;
using System.Threading.Tasks;

using AtariGo.Contracts;

namespace AtariGo.Client.Services;

public interface IGameSessionClient
{
    Task<IGameSessionStream> ConnectAsync(
        long gameId,
        CancellationToken cancellationToken);
}
