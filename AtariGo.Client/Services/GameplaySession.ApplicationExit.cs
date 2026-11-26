using System.Diagnostics;

using AtariGo.Client.Models;
using AtariGo.Contracts;

namespace AtariGo.Client.Services;

public sealed partial class GameplaySession
{
    private Task? _exitTask;
    private bool _isExiting;
    private bool _exitSurrenderAccepted;
    private TaskCompletionSource _exitStateChanged = CreateExitSignal();

    public Task SurrenderAndCloseAsync(CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            if (_exitTask is not null)
            {
                return _exitTask;
            }
            if (_isDisposed)
            {
                return Task.CompletedTask;
            }
            _isExiting = true;
            _exitTask = ExitApplicationAsync(cancellationToken);
            return _exitTask;
        }
    }

    private async Task ExitApplicationAsync(CancellationToken cancellationToken)
    {
        await Task.Yield();
        using CancellationTokenSource timeout = new CancellationTokenSource(gracefulCloseTimeout);
        using CancellationTokenSource exit = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, timeout.Token);
        try
        {
            bool canSurrender = await WaitForSurrenderSlotAsync(exit.Token).ConfigureAwait(false);
            if (canSurrender)
            {
                await SurrenderAsync(exit.Token).ConfigureAwait(false);
            }
            bool confirmed = await WaitForExitConfirmationAsync(exit.Token).ConfigureAwait(false);
            if (confirmed)
            {
                await CloseAsync(exit.Token).ConfigureAwait(false);
            }
            else
            {
                Trace.TraceInformation("Application exit has no confirmed game result. "
                    + "GameId: {0}.", _gameId);
            }
        }
        catch (OperationCanceledException) when (exit.IsCancellationRequested)
        {
            Trace.TraceInformation("Application exit confirmation timed out or was cancelled. "
                + "GameId: {0}.", _gameId);
        }
        catch (Exception exception) when (IsTransportFailure(exception))
        {
            Trace.TraceWarning("Application exit transport failed. GameId: {0}. Error: {1}.",
                _gameId, exception.GetType().Name);
        }
        finally
        {
            Dispose();
        }
    }

    private async Task<bool> WaitForSurrenderSlotAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            Task changed;
            lock (_sync)
            {
                if (_isDisposed || _state == GameplayConnectionState.Closed
                    || _isFinished || _exitSurrenderAccepted)
                {
                    return false;
                }
                if (_pendingAction is null)
                {
                    return true;
                }
                changed = _exitStateChanged.Task;
            }
            await changed.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<bool> WaitForExitConfirmationAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            Task changed;
            lock (_sync)
            {
                if (_isFinished || _exitSurrenderAccepted)
                {
                    return true;
                }
                if (_isDisposed || _state == GameplayConnectionState.Closed)
                {
                    return false;
                }
                changed = _exitStateChanged.Task;
            }
            await changed.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private bool CanQueueAction(GameSessionClientEvent action)
    {
        if (_isExiting)
        {
            return action.EventCase == GameSessionClientEvent.EventOneofCase.Surrender
                && _state != GameplayConnectionState.Closed;
        }
        return _state == GameplayConnectionState.Connected;
    }

    private void SignalExitStateChanged()
    {
        if (!_isExiting)
        {
            return;
        }
        TaskCompletionSource previous = _exitStateChanged;
        _exitStateChanged = CreateExitSignal();
        previous.TrySetResult();
    }

    private static TaskCompletionSource CreateExitSignal()
    {
        return new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
