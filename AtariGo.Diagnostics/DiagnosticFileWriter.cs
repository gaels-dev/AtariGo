using System.Threading.Channels;

namespace AtariGo.Diagnostics;

public sealed class DiagnosticFileWriter : IAsyncDisposable
{
    private readonly object _stateLock = new object();
    private readonly DiagnosticFileOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly DiagnosticFileStore _store;
    private readonly Channel<(byte[]? Record, TaskCompletionSource? Flushed)> _queue;
    private readonly CancellationTokenSource _abort;
    private readonly Task _worker;
    private DiagnosticStorageStatus _status = new DiagnosticStorageStatus();
    private Task? _stopping;
    private long _reportedRejected;
    private long _reportedFailures;

    public DiagnosticFileWriter(DiagnosticFileOptions options, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        _abort = new CancellationTokenSource();
        _options = options;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _store = new DiagnosticFileStore(options, _timeProvider);
        _queue = Channel.CreateBounded<(byte[]?, TaskCompletionSource?)>(options.QueueCapacity);
        _worker = Task.Run(ProcessAsync, CancellationToken.None);
    }

    public DiagnosticStorageStatus Status
    {
        get
        {
            lock (_stateLock)
            {
                return _status;
            }
        }
    }

    public bool TryWrite(DiagnosticEvent entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        byte[]? record = DiagnosticFileFormatter.CanFormat(entry)
            ? DiagnosticFileFormatter.Format(entry) : null;
        lock (_stateLock)
        {
            if (record is null || record.Length > _options.MaximumFileBytes
                || !_queue.Writer.TryWrite((record, null)))
            {
                _status = _status with { Rejected = _status.Rejected + 1 };
                return false;
            }
            _status = _status with { Accepted = _status.Accepted + 1 };
            return true;
        }
    }

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        var flushed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await _queue.Writer.WriteAsync((null, flushed), cancellationToken).ConfigureAwait(false);
        await flushed.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public ValueTask DisposeAsync()
    {
        lock (_stateLock)
        {
            _queue.Writer.TryComplete();
            _stopping ??= StopAsync();
            return new ValueTask(_stopping);
        }
    }

    private async Task ProcessAsync()
    {
        try
        {
            await foreach ((byte[]? Record, TaskCompletionSource? Flushed) request in
                _queue.Reader.ReadAllAsync(_abort.Token)
                .ConfigureAwait(false))
            {
                if (request.Record is null)
                {
                    request.Flushed!.TrySetResult();
                    continue;
                }
                await PersistAsync(request.Record).ConfigureAwait(false);
                await WriteSummaryAsync().ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (_abort.IsCancellationRequested)
        {
            MarkUnconfirmed();
        }
        catch (Exception failure)
        {
            RecordFailure(failure);
            throw;
        }
        finally
        {
            lock (_stateLock)
            {
                _queue.Writer.TryComplete();
            }
            MarkUnconfirmed();
            while (_queue.Reader.TryRead(
                out (byte[]? Record, TaskCompletionSource? Flushed) request))
            {
                request.Flushed?.TrySetCanceled(_abort.Token);
            }
            try
            {
                await _store.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
            {
                RecordFailure(failure);
            }
        }
    }

    private async Task PersistAsync(byte[] record)
    {
        while (!await TryAppendAsync(record).ConfigureAwait(false))
        {
            await Task.Delay(_options.RetryDelay, _timeProvider, _abort.Token)
                .ConfigureAwait(false);
        }
        lock (_stateLock)
        {
            _status = _status with { Written = _status.Written + 1, IsDegraded = false };
        }
    }

    private async Task<bool> TryAppendAsync(byte[] record, bool isSummary = false)
    {
        try
        {
            return await _store.AppendAsync(record, isSummary, _abort.Token).ConfigureAwait(false);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            RecordFailure(failure);
            return false;
        }
    }

    private void RecordFailure(Exception failure)
    {
        DiagnosticError error = DiagnosticError.FromException(failure);
        lock (_stateLock)
        {
            _status = _status with
            {
                WriteFailures = _status.WriteFailures + 1,
                IsDegraded = true,
                LastError = error
            };
        }
    }

    private async Task WriteSummaryAsync()
    {
        DiagnosticStorageStatus status = Status;
        if (status.Rejected == _reportedRejected && status.WriteFailures == _reportedFailures)
        {
            return;
        }
        byte[] summary = DiagnosticFileFormatter.FormatSummary(status, _timeProvider.GetUtcNow());
        if (summary.Length <= _options.MaximumFileBytes
            && await TryAppendAsync(summary, isSummary: true).ConfigureAwait(false))
        {
            _reportedRejected = status.Rejected;
            _reportedFailures = status.WriteFailures;
        }
    }

    private async Task StopAsync()
    {
        try
        {
            await using ITimer timeout = _timeProvider.CreateTimer(CancelPendingWrites, null,
                _options.ShutdownTimeout, Timeout.InfiniteTimeSpan);
            await _worker.ConfigureAwait(false);
        }
        finally
        {
            _abort.Dispose();
        }
    }

    private void CancelPendingWrites(object? state)
    {
        _abort.Cancel();
    }

    private void MarkUnconfirmed()
    {
        lock (_stateLock)
        {
            _status = _status with
            {
                Unconfirmed = _status.Accepted - _status.Written,
                IsDegraded = _status.IsDegraded || _status.Accepted != _status.Written
            };
        }
    }
}
