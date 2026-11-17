using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

using AtariGo.Server.Application.Authentication;
using AtariGo.Server.Data;
using AtariGo.Server.Data.Entities;

namespace AtariGo.Server.Application.PasswordRecovery;

public sealed class PasswordResetWriter
{
    private readonly AtariGoDbContext _dbContext;
    private readonly RecoveryCodeProtector _protector;
    private readonly IPasswordHasher<User> _hasher;
    private readonly RecoverySessionRevoker _revoker;
    private readonly TimeProvider _timeProvider;

    public PasswordResetWriter(AtariGoDbContext dbContext, RecoveryCodeProtector protector,
        IPasswordHasher<User> hasher, RecoverySessionRevoker revoker, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(protector);
        ArgumentNullException.ThrowIfNull(hasher);
        ArgumentNullException.ThrowIfNull(revoker);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _dbContext = dbContext;
        _protector = protector;
        _hasher = hasher;
        _revoker = revoker;
        _timeProvider = timeProvider;
    }

    public async Task<RecoveryResult> ResetAsync(PasswordResetCommand command,
        CancellationToken cancellationToken)
    {
        if (!_protector.IsConfigured)
        {
            return RecoveryResult.Unavailable;
        }
        RecoveryResult? validation = ValidatePassword(command);
        if (validation.HasValue)
        {
            return validation.Value;
        }
        await using IDbContextTransaction transaction = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken);
        var accounts = new AccountCredentialStore(_dbContext);
        User? user = await accounts.LockAccountAsync(command.Identifier, cancellationToken);
        if (user?.Credential is null)
        {
            return RecoveryResult.InvalidCode;
        }
        AuthenticationCode? code = await FindCodeAsync(user.UserId, cancellationToken);
        DateTime now = DateTime.UnixEpoch.AddSeconds(_timeProvider.GetUtcNow().ToUnixTimeSeconds());
        if (!IsUsable(code, now))
        {
            return RecoveryResult.InvalidCode;
        }
        if (!_protector.Matches(user.UserId, command.Code, code!.CodeHash))
        {
            code.AttemptCount++;
            if (code.AttemptCount == RecoveryCodeProtector.MaximumAttempts)
            {
                code.UsedAt = now;
            }
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return RecoveryResult.InvalidCode;
        }
        List<long> sessionIds = await RevokeSessionsAsync(user.UserId, now, cancellationToken);
        user.Credential.PasswordHash = _hasher.HashPassword(user, command.NewPassword);
        user.Credential.PasswordChangedAt = now;
        code.UsedAt = now;
        if (!await TrySaveAsync(transaction, cancellationToken))
        {
            return RecoveryResult.NotSaved;
        }
        await transaction.CommitAsync(cancellationToken);
        _revoker.Disconnect(user.UserId, sessionIds);
        return RecoveryResult.Reset;
    }

    private async Task<bool> TrySaveAsync(IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return false;
        }
        return true;
    }

    private Task<AuthenticationCode?> FindCodeAsync(int userId, CancellationToken cancellationToken)
    {
        return _dbContext.AuthenticationCodes
            .Where(code => code.UserId == userId && code.CodeType == RecoveryCodeProtector.Purpose)
            .OrderByDescending(code => code.AuthenticationCodeId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<List<long>> RevokeSessionsAsync(int userId, DateTime now,
        CancellationToken cancellationToken)
    {
        IQueryable<Session> sessions = _dbContext.Sessions.Where(session => session.UserId == userId
            && session.State == DatabaseValues.SessionStates.Active);
        List<long> ids = await sessions.Select(session => session.SessionId)
            .ToListAsync(cancellationToken);
        await sessions.ExecuteUpdateAsync(setters => setters
            .SetProperty(session => session.State, DatabaseValues.SessionStates.Revoked)
            .SetProperty(session => session.LastActivityAt, now), cancellationToken);
        return ids;
    }

    private static bool IsUsable(AuthenticationCode? code, DateTime now)
    {
        return code is not null && code.UsedAt is null && code.ExpiresAt > now
            && code.AttemptCount < RecoveryCodeProtector.MaximumAttempts;
    }

    private static RecoveryResult? ValidatePassword(PasswordResetCommand command)
    {
        if (!PasswordPolicy.IsValid(command.NewPassword))
        {
            return RecoveryResult.InvalidPassword;
        }
        return string.Equals(command.NewPassword, command.Confirmation, StringComparison.Ordinal)
            ? null : RecoveryResult.ConfirmationMismatch;
    }
}
