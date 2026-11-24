namespace AtariGo.Diagnostics;

internal sealed class DiagnosticFileStore : IAsyncDisposable
{
    private const int UnbufferedSize = 1;
    private const int NativeErrorMask = 65535;
    private const int SharingViolation = 32;
    private const int LockViolation = 33;
    private readonly DiagnosticFileOptions _options;
    private readonly DiagnosticFileRetention _retention;
    private FileStream? _stream;

    internal DiagnosticFileStore(DiagnosticFileOptions options, TimeProvider timeProvider)
    {
        _options = options;
        _retention = new DiagnosticFileRetention(options, timeProvider);
    }

    internal async Task<bool> AppendAsync(byte[] record, bool isSummary,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_options.DirectoryPath);
        using FileStream? coordination = TryCoordinate();
        if (coordination is null)
        {
            return false;
        }
        if (_stream is not null && _stream.Length > _options.MaximumFileBytes - record.Length)
        {
            if (isSummary)
            {
                return false;
            }
            await CloseAsync().ConfigureAwait(false);
        }
        if (!_retention.Prepare(record.Length, !isSummary))
        {
            if (isSummary)
            {
                return false;
            }
            throw new IOException("The diagnostic file budget is exhausted.");
        }
        _stream ??= OpenSegment();
        long position = _stream.Position;
        try
        {
            await _stream.WriteAsync(record, cancellationToken).ConfigureAwait(false);
            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception failure) when (failure is IOException or OperationCanceledException)
        {
            RollBack(position, failure);
            throw;
        }
        return true;
    }

    public ValueTask DisposeAsync()
    {
        return CloseAsync();
    }

    private FileStream? TryCoordinate()
    {
        string path = Path.Combine(_options.DirectoryPath, _options.FilePrefix + ".lock");
        try
        {
            return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite,
                FileShare.None);
        }
        catch (IOException failure)
            when ((failure.HResult & NativeErrorMask) is SharingViolation or LockViolation)
        {
            return null;
        }
    }

    private FileStream OpenSegment()
    {
        string fileName = $"{_options.FilePrefix}-{Guid.NewGuid():N}.log";
        return new FileStream(Path.Combine(_options.DirectoryPath, fileName), FileMode.CreateNew,
            FileAccess.Write, FileShare.Read, UnbufferedSize, FileOptions.Asynchronous);
    }

    private async ValueTask CloseAsync()
    {
        FileStream? stream = _stream;
        _stream = null;
        if (stream is not null)
        {
            await stream.DisposeAsync().ConfigureAwait(false);
        }
    }

    private void RollBack(long position, Exception failure)
    {
        try
        {
            _stream!.SetLength(position);
            _stream.Position = position;
        }
        catch (IOException rollbackFailure)
        {
            throw new AggregateException("The diagnostic write could not be rolled back.",
                failure, rollbackFailure);
        }
    }
}
