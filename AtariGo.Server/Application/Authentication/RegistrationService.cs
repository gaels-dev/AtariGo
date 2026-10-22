using AtariGo.Server.Data;
using AtariGo.Server.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AtariGo.Server.Application.Authentication;

public sealed class RegistrationService(
    AtariGoDbContext dbContext,
    IPasswordHasher<User> passwordHasher,
    IRegistrationValidator registrationValidator,
    ILogger<RegistrationService> logger) : IRegistrationService
{
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

        User user = CreateUser(command, playerRole);
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
        Role playerRole)
    {
        DateTime now = DateTime.Now;
        var user = new User
        {
            UserName = command.UserName.Trim(),
            Email = command.Email.Trim(),
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
