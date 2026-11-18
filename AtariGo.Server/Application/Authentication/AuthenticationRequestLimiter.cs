using System.Threading.RateLimiting;

using Microsoft.Extensions.Options;

using AtariGo.Server.Application.PasswordRecovery;

namespace AtariGo.Server.Application.Authentication;

public sealed class AuthenticationRequestLimiter : IDisposable
{
    private readonly PartitionedRateLimiter<string> _login;
    private readonly PartitionedRateLimiter<string> _recovery;

    public AuthenticationRequestLimiter(IOptions<LoginVerificationOptions> login,
        IOptions<PasswordRecoveryOptions> recovery)
    {
        ArgumentNullException.ThrowIfNull(login);
        ArgumentNullException.ThrowIfNull(recovery);
        _login = CreateLimiter(login.Value.IpRequestsPerHour);
        _recovery = CreateLimiter(recovery.Value.IpRequestsPerHour);
    }

    public bool TryAcquireLogin(string address)
    {
        return TryAcquire(_login, address);
    }

    public bool TryAcquireRecovery(string address)
    {
        return TryAcquire(_recovery, address);
    }

    public void Dispose()
    {
        _login.Dispose();
        _recovery.Dispose();
    }

    private static PartitionedRateLimiter<string> CreateLimiter(int limit)
    {
        return PartitionedRateLimiter.Create<string, string>(address =>
            RateLimitPartition.GetFixedWindowLimiter(address, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = limit, Window = AuthenticationCodeIssuanceService.RequestWindow,
                QueueLimit = 0, AutoReplenishment = true
            }));
    }

    private static bool TryAcquire(PartitionedRateLimiter<string> limiter, string address)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        using RateLimitLease lease = limiter.AttemptAcquire(address);
        return lease.IsAcquired;
    }
}
