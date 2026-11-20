using Microsoft.Extensions.Options;

using AtariGo.Server.Concurrency;

namespace AtariGo.Server.Application.Authentication;

public sealed class PendingLoginStore : IDisposable, IAsyncDisposable
{
    private const int GateCount = 128;
    private readonly Dictionary<string, PendingLogin> _pending =
        new Dictionary<string, PendingLogin>(StringComparer.Ordinal);
    private readonly AsyncOperationGate _transitions = new AsyncOperationGate(GateCount);
    private readonly object _sync = new object();
    private readonly TimeProvider _timeProvider;
    private readonly LoginVerificationOptions _options;
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

    public Task<AsyncOperationLease> EnterAsync(string tokenHash,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tokenHash);
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
        }
        return _transitions.AcquireAsync(StringComparer.Ordinal.GetHashCode(tokenHash),
            cancellationToken);
    }

    public void Dispose()
    {
        lock (_sync)
        {
            _isDisposed = true;
            _pending.Clear();
        }
        _transitions.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return _transitions.DisposeAsync();
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

}
