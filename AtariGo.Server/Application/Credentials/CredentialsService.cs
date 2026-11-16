using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using AtariGo.Server.Application.Authentication;
using AtariGo.Server.Application.Sessions;
using AtariGo.Server.Data;
using AtariGo.Server.Data.Entities;

namespace AtariGo.Server.Application.Credentials;

public sealed class CredentialsService : ICredentialsService
{
    private const string HashComparisonCollation = "Latin1_General_100_BIN2";

    private readonly AtariGoDbContext _dbContext;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly TimeProvider _timeProvider;

    public CredentialsService(AtariGoDbContext dbContext, IPasswordHasher<User> passwordHasher,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(passwordHasher);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _timeProvider = timeProvider;
    }

    public async Task<ChangePasswordResult> ChangePasswordAsync(SessionIdentity identity,
        ChangePasswordCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.CurrentPassword);
        ArgumentNullException.ThrowIfNull(command.NewPassword);
        ArgumentNullException.ThrowIfNull(command.Confirmation);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(identity.UserId);
        User? user = await _dbContext.Users.AsNoTracking()
            .Include(candidate => candidate.Credential)
            .SingleOrDefaultAsync(candidate => candidate.UserId == identity.UserId,
                cancellationToken);
        if (user?.Credential is null || user.State != DatabaseValues.AccountStates.Active)
        {
            return ChangePasswordResult.AccountUnavailable;
        }
        if (!VerifyCurrentPassword(user, command.CurrentPassword))
        {
            return ChangePasswordResult.IncorrectCurrentPassword;
        }
        if (!PasswordPolicy.IsValid(command.NewPassword))
        {
            return ChangePasswordResult.InvalidNewPassword;
        }
        if (!string.Equals(command.NewPassword, command.Confirmation, StringComparison.Ordinal))
        {
            return ChangePasswordResult.ConfirmationMismatch;
        }
        return await UpdateCredentialAsync(user, command.NewPassword, cancellationToken);
    }

    private bool VerifyCurrentPassword(User user, string password)
    {
        PasswordVerificationResult result;
        try
        {
            result = _passwordHasher.VerifyHashedPassword(user,
                user.Credential!.PasswordHash, password);
        }
        catch (FormatException)
        {
            result = PasswordVerificationResult.Failed;
        }
        return result != PasswordVerificationResult.Failed;
    }

    private async Task<ChangePasswordResult> UpdateCredentialAsync(User user, string newPassword,
        CancellationToken cancellationToken)
    {
        string originalHash = user.Credential!.PasswordHash;
        string newHash = _passwordHasher.HashPassword(user, newPassword);
        DateTime changedAt = _timeProvider.GetUtcNow().UtcDateTime;
        int updated = await _dbContext.Credentials
            .Where(credential => credential.UserId == user.UserId
                && EF.Functions.Collate(credential.PasswordHash, HashComparisonCollation)
                    == originalHash)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(credential => credential.PasswordHash, newHash)
                .SetProperty(credential => credential.PasswordChangedAt, changedAt),
                cancellationToken);
        return updated > 0 ? ChangePasswordResult.Changed : ChangePasswordResult.CredentialChanged;
    }
}
