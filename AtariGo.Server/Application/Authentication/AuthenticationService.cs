using AtariGo.Server.Data;
using AtariGo.Server.Data.Entities;
using AtariGo.Server.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AtariGo.Server.Application.Authentication;

public sealed class AuthenticationService(
    AtariGoDbContext dbContext,
    IPasswordHasher<User> passwordHasher,
    ISessionTokenProvider sessionTokenProvider,
    IRegistrationValidator registrationValidator,
    ILogger<AuthenticationService> logger) : IAuthenticationService
{
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(1);

    public async Task<LoginOutcome> LoginAsync(
        string identifier,
        string password,
        CancellationToken cancellationToken)
    {
        User? user = await FindUserByIdentifierAsync(identifier, cancellationToken);

        if (user?.Credential is null)
        {
            return new LoginOutcome(LoginStatus.UserNotFound);
        }

        if (!VerifyPassword(user, password))
        {
            return new LoginOutcome(LoginStatus.IncorrectPassword);
        }

        DateTime now = DateTime.Now;
        if (!CanAccessAccount(user, now))
        {
            return new LoginOutcome(LoginStatus.AccountRestricted);
        }

        IssuedSessionToken token = await CreateSessionAsync(user, now, cancellationToken);

        return new LoginOutcome(
            LoginStatus.Success,
            user.UserId,
            user.UserName,
            token.Value);
    }

    private Task<User?> FindUserByIdentifierAsync(
        string identifier,
        CancellationToken cancellationToken)
    {
        string normalizedIdentifier = identifier.Trim();

        return dbContext.Users
            .Include(candidate => candidate.Credential)
            .SingleOrDefaultAsync(
                candidate => candidate.UserName == normalizedIdentifier
                    || candidate.Email == normalizedIdentifier,
                cancellationToken);
    }

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
            credential.PasswordHash = passwordHasher.HashPassword(user, password);
        }

        return true;
    }

    private PasswordVerificationResult VerifyStoredPasswordHash(
        User user,
        string password)
    {
        try
        {
            return passwordHasher.VerifyHashedPassword(
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
        return string.Equals(user.State, "Active", StringComparison.OrdinalIgnoreCase)
            && (user.Credential!.LockedUntil is null
                || user.Credential.LockedUntil <= now);
    }

    private async Task<IssuedSessionToken> CreateSessionAsync(
        User user,
        DateTime now,
        CancellationToken cancellationToken)
    {
        IssuedSessionToken token = sessionTokenProvider.Create();
        dbContext.Sessions.Add(new Session
        {
            UserId = user.UserId,
            SessionTokenHash = token.Hash,
            StartedAt = now,
            LastActivityAt = now,
            ExpiresAt = now.Add(SessionLifetime),
            State = "Active"
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return token;
    }

    public async Task<RegistrationStatus> RegisterAccountAsync(
        RegisterAccountCommand command,
        CancellationToken cancellationToken)
    {
        if (!registrationValidator.IsValid(command))
        {
            return RegistrationStatus.Error;
        }

        string userName = command.UserName.Trim();
        string email = command.Email.Trim();
        RegistrationStatus? existingAccountStatus = await FindExistingAccountStatusAsync(
            userName,
            email,
            cancellationToken);

        if (existingAccountStatus.HasValue)
        {
            return existingAccountStatus.Value;
        }

        Role? playerRole = await FindPlayerRoleAsync(cancellationToken);
        if (playerRole is null)
        {
            return RegistrationStatus.Error;
        }

        User user = CreateUser(command, userName, email, playerRole);
        return await SaveNewUserAsync(user, cancellationToken);
    }

    private async Task<RegistrationStatus?> FindExistingAccountStatusAsync(
        string userName,
        string email,
        CancellationToken cancellationToken)
    {
        bool userNameExists = await dbContext.Users.AnyAsync(
            user => user.UserName == userName,
            cancellationToken);
        if (userNameExists)
        {
            return RegistrationStatus.UsernameTaken;
        }

        bool emailExists = await dbContext.Users.AnyAsync(
            user => user.Email == email,
            cancellationToken);
        return emailExists ? RegistrationStatus.EmailTaken : null;
    }

    private Task<Role?> FindPlayerRoleAsync(CancellationToken cancellationToken)
    {
        return dbContext.Roles.SingleOrDefaultAsync(
            role => role.Name == "Player",
            cancellationToken);
    }

    private User CreateUser(
        RegisterAccountCommand command,
        string userName,
        string email,
        Role playerRole)
    {
        DateTime now = DateTime.UtcNow;
        var user = new User
        {
            UserName = userName,
            Email = email,
            State = "Active",
            RegistrationDate = now,
            RoleId = playerRole.RoleId,
            Credential = new Credential { FailedAttempts = 0 },
            Profile = new Profile()
        };
        user.Credential!.PasswordHash = passwordHasher.HashPassword(user, command.Password);
        return user;
    }

    private async Task<RegistrationStatus> SaveNewUserAsync(
        User user,
        CancellationToken cancellationToken)
    {
        try
        {
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync(cancellationToken);
            return RegistrationStatus.Success;
        }
        catch (DbUpdateException exception)
        {
            logger.LogWarning(exception, "Could not register account.");
            dbContext.ChangeTracker.Clear();
            return RegistrationStatus.Error;
        }
    }
}
