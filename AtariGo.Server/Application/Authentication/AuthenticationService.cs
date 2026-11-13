using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using AtariGo.Server.Data;
using AtariGo.Server.Data.Entities;
using AtariGo.Server.Security;

namespace AtariGo.Server.Application.Authentication;

public sealed class AuthenticationService : IAuthenticationService
{
    private static readonly TimeSpan sessionLifetime = TimeSpan.FromHours(1);
    private readonly AtariGoDbContext _dbContext;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly ISessionTokenProvider _sessionTokenProvider;
    private readonly TimeProvider _timeProvider;

    public AuthenticationService(
        AtariGoDbContext dbContext,
        IPasswordHasher<User> passwordHasher,
        ISessionTokenProvider sessionTokenProvider,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(passwordHasher);
        ArgumentNullException.ThrowIfNull(sessionTokenProvider);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _sessionTokenProvider = sessionTokenProvider;
        _timeProvider = timeProvider;
    }

    public async Task<LoginOperationResult> LoginAsync(
        string identifier,
        string password,
        CancellationToken cancellationToken)
    {
        User? user = await FindUserByIdentifierAsync(identifier, cancellationToken);

        if (user?.Credential is null)
        {
            return new LoginOperationResult(LoginStatus.UserNotFound);
        }

        if (!VerifyPassword(user, password))
        {
            return new LoginOperationResult(LoginStatus.IncorrectPassword);
        }

        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        if (!CanAccessAccount(user, now))
        {
            return new LoginOperationResult(LoginStatus.AccountRestricted);
        }

        IssuedSessionToken token = await CreateSessionAsync(user, now, cancellationToken);

        return new LoginOperationResult(LoginStatus.Success)
        {
            UserId = user.UserId,
            UserName = user.UserName,
            SessionToken = token.Value
        };
    }

    private Task<User?> FindUserByIdentifierAsync(
        string identifier,
        CancellationToken cancellationToken)
    {
        string normalizedIdentifier = identifier.Trim();

        return _dbContext.Users
            .Include(candidate => candidate.Credential)
            .SingleOrDefaultAsync(
                candidate => candidate.UserName == normalizedIdentifier
                    || candidate.Email == normalizedIdentifier,
                cancellationToken);
    }

    /// <summary>Verifies a password and upgrades a legacy value after a successful match.</summary>
    private bool VerifyPassword(User user, string password)
    {
        Credential credential = user.Credential!;
        PasswordVerificationResult result = VerifyStoredPasswordHash(user, password);

        bool isLegacyPassword = result == PasswordVerificationResult.Failed
            && string.Equals(credential.PasswordHash, password, StringComparison.Ordinal);
        if (result == PasswordVerificationResult.Failed && !isLegacyPassword)
        {
            return false;
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded || isLegacyPassword)
        {
            credential.PasswordHash = _passwordHasher.HashPassword(user, password);
        }

        return true;
    }

    private PasswordVerificationResult VerifyStoredPasswordHash(
        User user,
        string password)
    {
        try
        {
            return _passwordHasher.VerifyHashedPassword(
                user,
                user.Credential!.PasswordHash,
                password);
        }
        catch (FormatException)
        {
            return PasswordVerificationResult.Failed;
        }
    }

    private static bool CanAccessAccount(User user, DateTime now)
    {
        bool isActive = string.Equals(
            user.State,
            DatabaseValues.AccountStates.Active,
            StringComparison.OrdinalIgnoreCase);
        bool isUnlocked = user.Credential!.LockedUntil is null
            || user.Credential.LockedUntil <= now;

        return isActive && isUnlocked;
    }

    private async Task<IssuedSessionToken> CreateSessionAsync(
        User user,
        DateTime now,
        CancellationToken cancellationToken)
    {
        IssuedSessionToken token = _sessionTokenProvider.Create();
        _dbContext.Sessions.Add(new Session
        {
            UserId = user.UserId,
            SessionTokenHash = token.Hash,
            StartedAt = now,
            LastActivityAt = now,
            ExpiresAt = now.Add(sessionLifetime),
            State = DatabaseValues.SessionStates.Active
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        return token;
    }
}
