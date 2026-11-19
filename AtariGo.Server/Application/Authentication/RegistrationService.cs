using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using AtariGo.Server.Data;
using AtariGo.Server.Data.Entities;

namespace AtariGo.Server.Application.Authentication;

public sealed class RegistrationService : IRegistrationService
{
    private readonly AtariGoDbContext _dbContext;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly RegistrationValidator _registrationValidator;
    private readonly ILogger<RegistrationService> _logger;
    private readonly TimeProvider _timeProvider;

    public RegistrationService(
        AtariGoDbContext dbContext,
        IPasswordHasher<User> passwordHasher,
        RegistrationValidator registrationValidator,
        ILogger<RegistrationService> logger,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(passwordHasher);
        ArgumentNullException.ThrowIfNull(registrationValidator);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _registrationValidator = registrationValidator;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    public async Task<RegistrationStatus> RegisterAccountAsync(
        RegisterAccountCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!_registrationValidator.IsValid(command))
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

        User user = CreateUser(command);
        return await SaveNewUserAsync(user, cancellationToken);
    }

    private async Task<RegistrationStatus?> FindExistingAccountStatusAsync(
        string userName,
        string email,
        CancellationToken cancellationToken)
    {
        bool userNameExists = await _dbContext.Users.AnyAsync(
            user => user.UserName == userName,
            cancellationToken);
        if (userNameExists)
        {
            return RegistrationStatus.UsernameTaken;
        }

        bool emailExists = await _dbContext.Users.AnyAsync(
            user => user.Email == email,
            cancellationToken);
        return emailExists ? RegistrationStatus.EmailTaken : null;
    }

    private User CreateUser(RegisterAccountCommand command)
    {
        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        var user = new User
        {
            UserName = command.UserName.Trim(),
            Email = command.Email.Trim(),
            State = DatabaseValues.AccountStates.Active,
            RegistrationDate = now,
            Credential = new Credential { FailedAttempts = 0 },
            Profile = new AtariGo.Server.Data.Entities.Profile
            {
                AvatarId = DatabaseValues.AvatarIds.Default
            }
        };
        user.Credential!.PasswordHash = _passwordHasher.HashPassword(
            user,
            command.Password);
        return user;
    }

    private async Task<RegistrationStatus> SaveNewUserAsync(
        User user,
        CancellationToken cancellationToken)
    {
        try
        {
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return RegistrationStatus.Success;
        }
        catch (DbUpdateException exception)
        {
            _logger.LogWarning("Could not register account: {ErrorType}.",
                exception.GetType().Name);
            _dbContext.ChangeTracker.Clear();
            return RegistrationStatus.Error;
        }
    }
}
