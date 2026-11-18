using System.Threading.RateLimiting;

using Microsoft.Extensions.Options;

namespace AtariGo.Server.Application.Authentication;

public sealed class LoginRequestLimiter : IDisposable
{
    private readonly PartitionedRateLimiter<string> _limiter;

    public LoginRequestLimiter(IOptions<LoginVerificationOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        int limit = options.Value.IpRequestsPerHour;
        _limiter = PartitionedRateLimiter.Create<string, string>(address =>
            RateLimitPartition.GetFixedWindowLimiter(address, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = limit, Window = TimeSpan.FromHours(1),
                QueueLimit = 0, AutoReplenishment = true
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
