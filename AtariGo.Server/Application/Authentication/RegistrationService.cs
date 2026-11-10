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

        Role? playerRole = await FindPlayerRoleAsync(cancellationToken);
        if (playerRole is null)
        {
            return RegistrationStatus.Error;
        }

        User user = CreateUser(command, playerRole);
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

    private Task<Role?> FindPlayerRoleAsync(CancellationToken cancellationToken)
    {
        return _dbContext.Roles.SingleOrDefaultAsync(
            role => role.Name == DatabaseValues.RoleNames.Player,
            cancellationToken);
    }

    private User CreateUser(
        RegisterAccountCommand command,
        Role playerRole)
    {
        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        var user = new User
        {
            UserName = command.UserName.Trim(),
            Email = command.Email.Trim(),
            State = DatabaseValues.AccountStates.Active,
            RegistrationDate = now,
            RoleId = playerRole.RoleId,
            Credential = new Credential { FailedAttempts = 0 },
            Profile = new Profile()
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
            _logger.LogWarning(exception, "Could not register account.");
            _dbContext.ChangeTracker.Clear();
            return RegistrationStatus.Error;
        }
    }
}
