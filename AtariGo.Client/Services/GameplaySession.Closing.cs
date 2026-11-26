using System.Diagnostics;

namespace AtariGo.Client.Services;

public sealed partial class GameplaySession
{
    private static readonly TimeSpan gracefulCloseTimeout = TimeSpan.FromSeconds(3);

    private Task? _closeTask;
    private bool _isClosing;

    public Task CloseAsync(CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            if (_closeTask is not null)
            {
                return _closeTask;
            }
            if (_isDisposed)
            {
                return Task.CompletedTask;
            }
            _isClosing = true;
            _pendingAction = null;
            _closeTask = CloseCoreAsync(cancellationToken);
            return _closeTask;
        }
    }

    private async Task CloseCoreAsync(CancellationToken cancellationToken)
    {
        await Task.Yield();
        using CancellationTokenSource timeout = new CancellationTokenSource(gracefulCloseTimeout);
        using CancellationTokenSource closing = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, timeout.Token);
        try
        {
            await CompleteConnectionAsync(closing.Token).ConfigureAwait(false);
            Trace.TraceInformation("Game session closed gracefully. GameId: {0}.", _gameId);
        }
        catch (OperationCanceledException) when (closing.IsCancellationRequested)
        {
            Trace.TraceInformation("Game session close cancelled or timed out. GameId: {0}.",
                _gameId);
        }
        catch (Exception exception) when (IsTransportFailure(exception))
        {
            Trace.TraceWarning("Game session close interrupted. GameId: {0}. Error: {1}.",
                _gameId, exception.GetType().Name);
        }
        finally
        {
            Dispose();
        }
    }

    private async Task CompleteConnectionAsync(CancellationToken cancellationToken)
    {
        IGameSessionStream? stream;
        Task? running;
        lock (_sync)
        {
            stream = _stream;
            running = _runTask;
        }
        if (stream is not null)
        {
            await stream.CompleteAsync(cancellationToken).ConfigureAwait(false);
        }
        if (running is not null)
        {
            await running.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
