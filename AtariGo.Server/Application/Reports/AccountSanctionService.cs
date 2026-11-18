using Microsoft.EntityFrameworkCore;

using AtariGo.Server.Data;

namespace AtariGo.Server.Application.Reports;

public sealed class AccountSanctionService : IAccountSanctionService
{
    private readonly AtariGoDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public AccountSanctionService(AtariGoDbContext dbContext, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public Task<bool> HasActiveSuspensionAsync(int userId, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);
        DateTime now = DateTime.UnixEpoch.AddSeconds(
            _timeProvider.GetUtcNow().ToUnixTimeSeconds());
        return _dbContext.Sanctions.AsNoTracking().AnyAsync(sanction =>
            sanction.UserId == userId
            && sanction.SanctionType == DatabaseValues.SanctionTypes.TemporarySuspension
            && sanction.RevokedAt == null && sanction.StartedAt <= now
            && sanction.EndsAt > now, cancellationToken);
    }
}
