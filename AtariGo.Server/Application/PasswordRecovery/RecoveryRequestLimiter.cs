using System.Threading.RateLimiting;

using Microsoft.Extensions.Options;

namespace AtariGo.Server.Application.PasswordRecovery;

public sealed class RecoveryRequestLimiter : IDisposable
{
    public static TimeSpan RequestWindow { get; } = TimeSpan.FromHours(1);

    private readonly PartitionedRateLimiter<string> _limiter;

    public RecoveryRequestLimiter(IOptions<PasswordRecoveryOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        int limit = options.Value.IpRequestsPerHour;
        _limiter = PartitionedRateLimiter.Create<string, string>(address =>
            RateLimitPartition.GetFixedWindowLimiter(address, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = limit,
                Window = RequestWindow,
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    }

    public bool TryAcquire(string address)
    {
        using RateLimitLease lease = _limiter.AttemptAcquire(address);
        return lease.IsAcquired;
    }

    public void Dispose()
    {
        _limiter.Dispose();
    }
}
