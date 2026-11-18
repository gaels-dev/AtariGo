using Microsoft.Extensions.Options;

namespace AtariGo.Server.Application.Authentication;

public sealed class PendingLoginStore : IDisposable, IAsyncDisposable
{
    private const int GateCount = 128;
    private readonly Dictionary<string, PendingLogin> _pending =
        new Dictionary<string, PendingLogin>(StringComparer.Ordinal);
    private readonly SemaphoreSlim[] _transitionGates = CreateGates();
    private readonly object _sync = new object();
    private readonly TaskCompletionSource _disposalCompletion = new TaskCompletionSource(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TimeProvider _timeProvider;
    private readonly LoginVerificationOptions _options;
    private int _pendingOperations;
    private bool _isDisposed;

    public PendingLoginStore(TimeProvider timeProvider, IOptions<LoginVerificationOptions> options)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        _timeProvider = timeProvider;
        _options = options.Value;
    }

    public bool TryAdd(PendingLogin pending)
    {
        ArgumentNullException.ThrowIfNull(pending);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pending.UserId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pending.CodeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(pending.TokenHash);
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            RemoveExpired();
            return _pending.Count < _options.MaximumPendingLogins
                && _pending.TryAdd(pending.TokenHash, pending);
        }
    }

    public PendingLogin? Find(string tokenHash)
    {
        ArgumentNullException.ThrowIfNull(tokenHash);
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            _pending.TryGetValue(tokenHash, out PendingLogin? pending);
            if (pending is not null && IsExpired(pending))
            {
                _pending.Remove(tokenHash);
                pending = null;
            }
            return pending;
        }
    }

    public void Publish(PendingLogin pending)
    {
        ArgumentNullException.ThrowIfNull(pending);
        lock (_sync)
        {
            if (pending.IsClosed || IsExpired(pending))
            {
                _pending.Remove(pending.TokenHash);
            }
            else if (_pending.ContainsKey(pending.TokenHash))
            {
                _pending[pending.TokenHash] = pending;
            }
        }
    }

    public async Task<PendingLoginLease> EnterAsync(string tokenHash,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tokenHash);
        int index = (int)((uint)StringComparer.Ordinal.GetHashCode(tokenHash) % GateCount);
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            _pendingOperations++;
        }
        try
        {
            await _transitionGates[index].WaitAsync(cancellationToken);
        }
        catch
        {
            FinishOperation();
            throw;
        }
        return new PendingLoginLease(this, index);
    }

    public void Dispose()
    {
        lock (_sync)
        {
            _isDisposed = true;
            TryFinishDisposal();
        }
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return new ValueTask(_disposalCompletion.Task);
    }

    internal void Exit(int gateIndex)
    {
        _transitionGates[gateIndex].Release();
        FinishOperation();
    }

    private static SemaphoreSlim[] CreateGates()
    {
        SemaphoreSlim[] gates = new SemaphoreSlim[GateCount];
        for (int index = 0; index < gates.Length; index++)
        {
            gates[index] = new SemaphoreSlim(1, 1);
        }
        return gates;
    }

    private bool IsExpired(PendingLogin pending)
    {
        return pending.IssueUntil.Add(LoginVerificationOptions.CodeLifetime)
            <= _timeProvider.GetUtcNow().UtcDateTime;
    }

    private void RemoveExpired()
    {
        string[] expired = _pending.Values.Where(IsExpired)
            .Select(pending => pending.TokenHash).ToArray();
        foreach (string tokenHash in expired)
        {
            _pending.Remove(tokenHash);
        }
    }

    private void FinishOperation()
    {
        lock (_sync)
        {
            _pendingOperations--;
            TryFinishDisposal();
        }
    }

    private void TryFinishDisposal()
    {
        if (_isDisposed && _pendingOperations == 0 && !_disposalCompletion.Task.IsCompleted)
        {
            foreach (SemaphoreSlim gate in _transitionGates)
            {
                gate.Dispose();
            }
            _pending.Clear();
            _disposalCompletion.SetResult();
        }
    }
}
