using Microsoft.EntityFrameworkCore;

using AtariGo.Server.Data;
using AtariGo.Server.Data.Entities;

namespace AtariGo.Server.Application.Authentication;

public sealed class LoginAttemptService
{
    private readonly AtariGoDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public LoginAttemptService(AtariGoDbContext dbContext, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public DateTime UtcNow =>
        DateTime.UnixEpoch.AddSeconds(_timeProvider.GetUtcNow().ToUnixTimeSeconds());

    public static void ResetExpiredLock(Credential credential, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(credential);
        if (credential.LockedUntil is not null && credential.LockedUntil <= now)
        {
            credential.LockedUntil = null;
            credential.FailedAttempts = 0;
        }
    }

    public async Task<LoginOperationResult?> CheckAccessAsync(User user,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        Credential credential = user.Credential
            ?? throw new InvalidOperationException("Account credentials are required.");
        DateTime now = UtcNow;
        ResetExpiredLock(credential, now);
        if (credential.LockedUntil > now)
        {
            return new LoginOperationResult(LoginStatus.TemporarilyLocked)
            {
                RetryAt = credential.LockedUntil
            };
        }
        Sanction? sanction = await _dbContext.Sanctions.AsNoTracking()
            .Include(item => item.TriggeringReport)
            .Where(item => item.UserId == user.UserId && item.RevokedAt == null
                && item.StartedAt <= now && item.EndsAt > now
                && item.SanctionType == DatabaseValues.SanctionTypes.TemporarySuspension)
            .OrderByDescending(item => item.EndsAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (sanction is not null)
        {
            return new LoginOperationResult(LoginStatus.AccountRestricted)
            {
                RestrictionReason = sanction.TriggeringReport.Category,
                RetryAt = sanction.EndsAt
            };
        }
        return user.State == DatabaseValues.AccountStates.Active
            ? null : new LoginOperationResult(LoginStatus.AccountRestricted);
    }

    public async Task RegisterFailureAsync(User user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        Credential credential = user.Credential
            ?? throw new InvalidOperationException("Account credentials are required.");
        DateTime now = UtcNow;
        ResetExpiredLock(credential, now);
        if (credential.LockedUntil > now)
        {
            return;
        }
        credential.FailedAttempts++;
        if (credential.FailedAttempts >= LoginVerificationOptions.MaximumFailedAttempts)
        {
            credential.LockedUntil = now.Add(LoginVerificationOptions.LockDuration);
            await _dbContext.AuthenticationCodes.Where(code => code.UserId == user.UserId
                && code.CodeType == LoginVerificationOptions.Purpose && code.UsedAt == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(code => code.UsedAt, now),
                    cancellationToken);
        }
    }
}
