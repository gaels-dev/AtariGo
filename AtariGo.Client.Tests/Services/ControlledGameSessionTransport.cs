using Grpc.Core;

using AtariGo.Contracts;

namespace AtariGo.Client.Tests.Services;

internal sealed class ControlledGameSessionTransport :
    IClientStreamWriter<GameSessionClientEvent>, IAsyncStreamReader<GameSessionServerEvent>
{
    private readonly TaskCompletionSource _started = new TaskCompletionSource(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _release = new TaskCompletionSource(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private int _writeCount;
    private int _disposeCount;

    public Task FirstWriteStarted => _started.Task;

    public int WriteCount => Volatile.Read(ref _writeCount);

    public int DisposeCount => Volatile.Read(ref _disposeCount);

    public Exception? FirstWriteFailure
    {
        get; set;
    }

    public WriteOptions? WriteOptions
    {
        get; set;
    }

    public GameSessionServerEvent Current { get; } = new GameSessionServerEvent();

    public AsyncDuplexStreamingCall<GameSessionClientEvent, GameSessionServerEvent> CreateCall()
    {
        return new AsyncDuplexStreamingCall<GameSessionClientEvent, GameSessionServerEvent>(
            this, this, Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess, () => new Metadata(), Dispose);
    }

    public void CompleteFirstWrite()
    {
        _release.TrySetResult();
    }

    public Task WriteAsync(GameSessionClientEvent message)
    {
        return WriteAsync(message, CancellationToken.None);
    }

    public async Task WriteAsync(GameSessionClientEvent message,
        CancellationToken cancellationToken)
    {
        if (Interlocked.Increment(ref _writeCount) == 1)
        {
            _started.TrySetResult();
            await _release.Task.WaitAsync(cancellationToken);
            if (FirstWriteFailure is not null)
            {
                throw FirstWriteFailure;
            }
        }
    }

    public Task CompleteAsync()
    {
        return Task.CompletedTask;
    }

    public Task<bool> MoveNext(CancellationToken cancellationToken)
    {
        return Task.FromResult(false);
    }

    public void Dispose()
    {
        Interlocked.Increment(ref _disposeCount);
    }
}
