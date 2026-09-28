using System.Security.Cryptography;
using System.Text;
using System.Net.Mail;
using AtariGo.Server.Models;
using AtariGo.Contracts;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;

namespace AtariGo.Server.Services;

public sealed class AuthenticationGrpcService(
    AtariGoDbContext dbContext,
    ILogger<AuthenticationGrpcService> logger)
    : Authentication.AuthenticationBase
{
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(8);

    public override async Task<LoginResponse> Login(
        LoginRequest request,
        ServerCallContext context)
    {
        string identifier = request.Identifier.Trim();

        var user = await dbContext.Users
            .AsNoTracking()
            .Include(candidate => candidate.Credential)
            .SingleOrDefaultAsync(
                candidate => candidate.UserName == identifier || candidate.Email == identifier,
                context.CancellationToken);

        if (user?.Credential is null)
        {
            return new LoginResponse { Result = LoginResult.UserNotFound };
        }

        // Current test data stores plain text in PasswordHash. Replace this comparison
        // with a password-hash verifier before using real accounts.
        if (!string.Equals(
                user.Credential.PasswordHash,
                request.Password,
                StringComparison.Ordinal))
        {
            return new LoginResponse { Result = LoginResult.IncorrectPassword };
        }

        DateTime now = DateTime.UtcNow;
        if (!string.Equals(user.State, "Active", StringComparison.OrdinalIgnoreCase)
            || user.Credential.LockedUntil > now)
        {
            return new LoginResponse { Result = LoginResult.AccountRestricted };
        }

        string sessionToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        string sessionTokenHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(sessionToken)));

        dbContext.Sessions.Add(new Session
        {
            UserId = user.UserId,
            SessionTokenHash = sessionTokenHash,
            StartedAt = now,
            LastActivityAt = now,
            ExpiresAt = now.Add(SessionLifetime),
            State = "Active"
        });

        await dbContext.SaveChangesAsync(context.CancellationToken);

        return new LoginResponse
        {
            Result = LoginResult.Success,
            UserId = user.UserId,
            UserName = user.UserName,
            SessionToken = sessionToken
        };
    }

    public override async Task<RegisterAccountResponse> RegisterAccount(
        RegisterAccountRequest request,
        ServerCallContext context)
    {
        string userName = request.UserName.Trim();
        string email = request.Email.Trim();

        if (string.IsNullOrWhiteSpace(userName)
            || userName.Length > 100
            || string.IsNullOrWhiteSpace(email)
            || email.Length > 254
            || !MailAddress.TryCreate(email, out _)
            || string.IsNullOrWhiteSpace(request.Password)
            || request.Password.Length < 8
            || !request.Password.Any(char.IsUpper)
            || request.Password.Length > 255)
        {
            return new RegisterAccountResponse { Result = RegistrationResult.Error };
        }

        if (!string.Equals(
                request.Password,
                request.PasswordConfirmation,
                StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(request.ConfirmationText))
        {
            return new RegisterAccountResponse { Result = RegistrationResult.Error };
        }

        bool userNameExists = await dbContext.Users.AnyAsync(
            user => user.UserName == userName,
            context.CancellationToken);
        if (userNameExists)
        {
            return new RegisterAccountResponse
            {
                Result = RegistrationResult.UsernameTaken
            };
        }

        bool emailExists = await dbContext.Users.AnyAsync(
            user => user.Email == email,
            context.CancellationToken);
        if (emailExists)
        {
            return new RegisterAccountResponse { Result = RegistrationResult.EmailTaken };
        }

        var playerRole = await dbContext.Roles.SingleOrDefaultAsync(
            role => role.Name == "Player",
            context.CancellationToken);
        if (playerRole is null)
        {
            return new RegisterAccountResponse { Result = RegistrationResult.Error };
        }

        DateTime now = DateTime.UtcNow;
        var user = new User
        {
            UserName = userName,
            Email = email,
            State = "Active",
            RegistrationDate = now,
            RoleId = playerRole.RoleId,
            Credential = new Credential
            {
                // Test-only: replace with a password hash before using real accounts.
                PasswordHash = request.Password,
                FailedAttempts = 0
            },
            Profile = new Profile()
        };

        try
        {
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync(context.CancellationToken);

            return new RegisterAccountResponse { Result = RegistrationResult.Success };
        }
        catch (DbUpdateException exception)
        {
            logger.LogWarning(exception, "Could not register account.");
            dbContext.ChangeTracker.Clear();
            return new RegisterAccountResponse { Result = RegistrationResult.Error };
        }
    }
}
