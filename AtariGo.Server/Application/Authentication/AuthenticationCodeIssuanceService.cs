using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using AtariGo.Server.Application.PasswordRecovery;
using AtariGo.Server.Data;

namespace AtariGo.Server.Application.Authentication;

public sealed class AuthenticationCodeIssuanceService
{
    private readonly AtariGoDbContext _dbContext;
    private readonly LoginVerificationOptions _login;
    private readonly PasswordRecoveryOptions _recovery;
    private readonly TimeProvider _timeProvider;

    public AuthenticationCodeIssuanceService(AtariGoDbContext dbContext,
        IOptions<LoginVerificationOptions> login, IOptions<PasswordRecoveryOptions> recovery,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(login);
        ArgumentNullException.ThrowIfNull(recovery);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _dbContext = dbContext;
        _login = login.Value;
        _recovery = recovery.Value;
        _timeProvider = timeProvider;
    }

    public static TimeSpan RequestWindow { get; } = TimeSpan.FromHours(1);

    public TimeSpan LoginResendDelay => TimeSpan.FromSeconds(_login.ResendSeconds);

    public Task<DateTime?> FindLoginRetryAtAsync(int userId, CancellationToken cancellationToken)
    {
        return FindRetryAtAsync(userId, (LoginVerificationOptions.Purpose,
            _login.AccountRequestsPerHour, _login.ResendSeconds), cancellationToken);
    }

    public Task<DateTime?> FindRecoveryRetryAtAsync(int userId, CancellationToken cancellationToken)
    {
        return FindRetryAtAsync(userId, (RecoveryCodeProtector.Purpose,
            _recovery.AccountRequestsPerHour, _recovery.ResendSeconds), cancellationToken);
    }

    private async Task<DateTime?> FindRetryAtAsync(int userId,
        (string Purpose, int Limit, int DelaySeconds) policy, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);
        DateTime now = DateTime.UnixEpoch.AddSeconds(_timeProvider.GetUtcNow().ToUnixTimeSeconds());
        DateTime windowStart = now.Subtract(RequestWindow);
        List<DateTime> issued = await _dbContext.AuthenticationCodes.AsNoTracking()
            .Where(code => code.UserId == userId && code.CodeType == policy.Purpose
                && code.CreatedAt > windowStart)
            .OrderBy(code => code.CreatedAt).Select(code => code.CreatedAt)
            .ToListAsync(cancellationToken);
        if (issued.Count >= policy.Limit)
        {
            return issued[0].Add(RequestWindow);
        }
        DateTime? last = issued.Count > 0 ? issued[^1] : null;
        DateTime? next = last?.AddSeconds(policy.DelaySeconds);
        return next > now ? next : null;
    }
}
