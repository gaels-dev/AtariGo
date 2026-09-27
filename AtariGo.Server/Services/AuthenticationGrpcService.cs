using System.Security.Cryptography;
using System.Text;
using AtariGo.Server.Models;
using AtariGo.Contracts;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;

namespace AtariGo.Server.Services;

public sealed class AuthenticationGrpcService(AtariGoDbContext dbContext)
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
}
