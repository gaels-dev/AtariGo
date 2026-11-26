using System.Runtime.CompilerServices;

using Grpc.Core;

using AtariGo.Contracts;

namespace AtariGo.Client.Services;

public sealed class GrpcGameSessionStream : IGameSessionStream
{
    private readonly SemaphoreSlim _writeGate = new SemaphoreSlim(1, 1);
    private readonly object _lifetimeSync = new object();
    private readonly TaskCompletionSource _disposalCompletion = new TaskCompletionSource(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _sendsDrained = new TaskCompletionSource(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private Task? _completeTask;
    private int _pendingSends;
    private bool _isDisposed;
    private bool _isTransportClosed;
    private bool _isCompleting;
    private bool _pendingCompletion;
    private readonly AsyncDuplexStreamingCall<GameSessionClientEvent,
        GameSessionServerEvent> _call;

    public GrpcGameSessionStream(
        AsyncDuplexStreamingCall<GameSessionClientEvent, GameSessionServerEvent> call)
    {
        ArgumentNullException.ThrowIfNull(call);
        _call = call;
    }

    public async IAsyncEnumerable<GameSessionServerEvent> ReadEventsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (await _call.ResponseStream.MoveNext(cancellationToken))
        {
            yield return _call.ResponseStream.Current;
        }
    }

    public Task SendChatMessageAsync(
        string text,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);
        cancellationToken.ThrowIfCancellationRequested();
        GameSessionClientEvent clientEvent = new GameSessionClientEvent
        {
            SendChatMessage = new SendChatMessage { Text = text }
        };
        return SendEventAsync(clientEvent, cancellationToken);
    }

    public async Task SendEventAsync(GameSessionClientEvent clientEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(clientEvent);
        await EnterWriteAsync(cancellationToken);
        try
        {
            lock (_lifetimeSync)
            {
                ObjectDisposedException.ThrowIf(_isDisposed, this);
            }
            await _call.RequestStream.WriteAsync(clientEvent, cancellationToken);
        }
        finally
        {
            _writeGate.Release();
            FinishSend();
        }
    }

    public Task CompleteAsync(CancellationToken cancellationToken)
    {
        lock (_lifetimeSync)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            if (_completeTask is not null)
            {
                return _completeTask;
            }
            _isCompleting = true;
            _pendingCompletion = true;
            if (_pendingSends == 0)
            {
                _sendsDrained.TrySetResult();
            }
            _completeTask = CompleteCoreAsync(cancellationToken);
            return _completeTask;
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (_lifetimeSync)
        {
            if (_isDisposed)
            {
                return new ValueTask(_disposalCompletion.Task);
            }
            _isDisposed = true;
        }
        try
        {
            _call.Dispose();
        }
        finally
        {
            lock (_lifetimeSync)
            {
                _isTransportClosed = true;
                TryFinishDisposal();
            }
        }
        return new ValueTask(_disposalCompletion.Task);
    }

    private async Task EnterWriteAsync(CancellationToken cancellationToken)
    {
        lock (_lifetimeSync)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            if (_isCompleting)
            {
                throw new InvalidOperationException("The game request stream is completing.");
            }
            _pendingSends++;
        }
        try
        {
            await _writeGate.WaitAsync(cancellationToken);
        }
        catch
        {
            FinishSend();
            throw;
        }
    }

    private void FinishSend()
    {
        lock (_lifetimeSync)
        {
            _pendingSends--;
            if (_pendingSends == 0 && _isCompleting)
            {
                _sendsDrained.TrySetResult();
            }
            TryFinishDisposal();
        }
    }

    private void TryFinishDisposal()
    {
        if (_isTransportClosed && _pendingSends == 0 && !_pendingCompletion
            && !_disposalCompletion.Task.IsCompleted)
        {
            _writeGate.Dispose();
            _disposalCompletion.SetResult();
        }
    }

    private async Task CompleteCoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _sendsDrained.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            await CompleteRequestAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            lock (_lifetimeSync)
            {
                _pendingCompletion = false;
                TryFinishDisposal();
            }
        }
    }

    private async Task CompleteRequestAsync(CancellationToken cancellationToken)
    {
        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            lock (_lifetimeSync)
            {
                ObjectDisposedException.ThrowIf(_isDisposed, this);
            }
            await _call.RequestStream.CompleteAsync().WaitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _writeGate.Release();
        }
    }
}
