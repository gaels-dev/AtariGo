using AtariGo.Client.Models;
using AtariGo.Client.Services;

namespace AtariGo.Client.Tests.Services;

public sealed class StubGameHistoryClient : IGameHistoryClient
{
    public Task<GameHistoryQueryResult> Response { get; set; } =
        Task.FromResult(new GameHistoryQueryResult());

    public CancellationToken RequestToken { get; private set; }

    public Task<GameHistoryQueryResult> ListGamesByPlayerAsync(CancellationToken cancellationToken)
    {
        RequestToken = cancellationToken;
        return Response;
    }
}
