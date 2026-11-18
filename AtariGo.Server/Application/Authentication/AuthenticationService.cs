using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Storage;

using AtariGo.Server.Data;
using AtariGo.Server.Data.Entities;

namespace AtariGo.Server.Application.Authentication;

public sealed class AuthenticationService : IAuthenticationService
{
    private readonly AtariGoDbContext _dbContext;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly LoginAttemptService _attempts;
    private readonly LoginChallengeService _challenges;

    public AuthenticationService(AtariGoDbContext dbContext, IPasswordHasher<User> passwordHasher,
        LoginAttemptService attempts, LoginChallengeService challenges)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(passwordHasher);
        ArgumentNullException.ThrowIfNull(attempts);
        ArgumentNullException.ThrowIfNull(challenges);
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _attempts = attempts;
        _challenges = challenges;
    }

    public async Task<LoginOperationResult> LoginAsync(LoginCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        _dbContext.ChangeTracker.Clear();
        LoginPreparation preparation;
        try
        {
            await using (IDbContextTransaction transaction = await _dbContext.Database
                .BeginTransactionAsync(cancellationToken))
            {
                preparation = await PrepareAsync(command, cancellationToken);
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
        }
        finally
        {
            command.Password = string.Empty;
        }
        return await _challenges.SendAsync(preparation, cancellationToken);
    }

    public Task<LoginOperationResult> VerifyAsync(LoginCodeCommand command,
        CancellationToken cancellationToken)
    {
        return _challenges.VerifyAsync(command, cancellationToken);
    }

    public Task<LoginOperationResult> ResendAsync(LoginChallengeCommand command,
        CancellationToken cancellationToken)
    {
        return _challenges.ResendAsync(command, cancellationToken);
    }

    public Task CancelAsync(string token, CancellationToken cancellationToken)
    {
        return _challenges.CancelAsync(token, cancellationToken);
    }

    private async Task<LoginPreparation> PrepareAsync(LoginCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Identifier) || string.IsNullOrEmpty(command.Password))
        {
            return new LoginPreparation(new LoginOperationResult(LoginStatus.InvalidCredentials));
        }
        var accounts = new AccountCredentialStore(_dbContext);
        User? user = await accounts.LockAccountAsync(command.Identifier, cancellationToken);
        if (user?.Credential is null)
        {
            return new LoginPreparation(new LoginOperationResult(LoginStatus.InvalidCredentials));
        }
        LoginAttemptService.ResetExpiredLock(user.Credential, _attempts.UtcNow);
        if (command.Password.Length > PasswordPolicy.MaximumPasswordLength
            || !VerifyPassword(user, command.Password))
        {
            await _attempts.RegisterFailureAsync(user, cancellationToken);
            return new LoginPreparation(new LoginOperationResult(LoginStatus.InvalidCredentials));
        }
        LoginOperationResult? restriction = await _attempts.CheckAccessAsync(user,
            cancellationToken);
        if (restriction is not null)
        {
            return new LoginPreparation(restriction);
        }
        return await _challenges.StartAsync(user, command.CultureName, cancellationToken);
    }

    private bool VerifyPassword(User user, string password)
    {
        Credential credential = user.Credential
            ?? throw new InvalidOperationException("Account credentials are required.");
        PasswordVerificationResult result;
        try
        {
            result = _passwordHasher.VerifyHashedPassword(user, credential.PasswordHash, password);
        }
        catch (FormatException)
        {
            result = PasswordVerificationResult.Failed;
        }
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            credential.PasswordHash = _passwordHasher.HashPassword(user, password);
        }
        return result != PasswordVerificationResult.Failed;
    }
}
