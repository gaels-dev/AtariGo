using AtariGo.Client.Models;

namespace AtariGo.Client.Services;

public interface IGameplaySession : IDisposable
{
    event Action<GameplaySessionUpdate>? Updated;

    Task RunAsync(long gameId, CancellationToken cancellationToken);

    Task CloseAsync(CancellationToken cancellationToken);

    Task SurrenderAndCloseAsync(CancellationToken cancellationToken);

    Task SubmitMoveAsync(int row, int column, CancellationToken cancellationToken);

    Task SurrenderAsync(CancellationToken cancellationToken);

    Task<bool> SendChatAsync(string text, CancellationToken cancellationToken);

    void RetryConnection();
}
