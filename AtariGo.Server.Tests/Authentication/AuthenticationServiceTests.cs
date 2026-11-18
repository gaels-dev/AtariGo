using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using AtariGo.Server.Application.Authentication;
using AtariGo.Server.Data;
using AtariGo.Server.Data.Entities;
using AtariGo.Server.Security;
using AtariGo.Server.Tests.Games;

namespace AtariGo.Server.Tests.Authentication;

[TestClass]
[TestCategory("Integration")]
public sealed class AuthenticationServiceTests
{
    private const string Password = "OriginalSecret8";

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task LoginAsync_StoredCredentialAsPassword_RejectsWithoutCreatingSession(
        bool storesPlaintext)
    {
        // Arrange
        await using SqlServerTestDatabase database = await SqlServerTestDatabase.CreateAsync();
        await using AtariGoDbContext context = database.CreateContext();
        User user = CreateUser();
        string stored = storesPlaintext ? Password : new PasswordHasher<User>()
            .HashPassword(user, Password);
        user.Credential = new Credential { PasswordHash = stored };
        await SeedAsync(context, user);
        AuthenticationService service = CreateService(context);
        // Act
        LoginOperationResult result = await service.LoginAsync(new LoginCommand
        {
            Identifier = user.UserName, Password = stored
        },
            CancellationToken.None);
        // Assert
        Assert.AreEqual(LoginStatus.InvalidCredentials, result.Status);
        Assert.AreEqual(string.Empty, result.SessionToken);
        Assert.AreEqual(0, await context.Sessions.CountAsync());
        Assert.AreEqual(stored, await context.Credentials.AsNoTracking()
            .Select(credential => credential.PasswordHash).SingleAsync());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task LoginAsync_VerifiedPassword_RequiresCodeAndRefreshesHashWhenNeeded(
        bool usesOlderHash)
    {
        // Arrange
        await using SqlServerTestDatabase database = await SqlServerTestDatabase.CreateAsync();
        await using AtariGoDbContext context = database.CreateContext();
        User user = CreateUser();
        var hasher = new PasswordHasher<User>();
        PasswordHasher<User> originalHasher = usesOlderHash
            ? new PasswordHasher<User>(Options.Create(new PasswordHasherOptions
            {
                CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV2
            })) : hasher;
        string originalHash = originalHasher.HashPassword(user, Password);
        user.Credential = new Credential { PasswordHash = originalHash };
        await SeedAsync(context, user);
        AuthenticationService service = CreateService(context);
        // Act
        LoginOperationResult result = await service.LoginAsync(new LoginCommand
        {
            Identifier = user.Email, Password = Password
        },
            CancellationToken.None);
        string savedHash = await context.Credentials.AsNoTracking()
            .Select(credential => credential.PasswordHash).SingleAsync();
        // Assert
        Assert.AreEqual(LoginStatus.ChallengeRequired, result.Status);
        Assert.AreEqual(string.Empty, result.SessionToken);
        Assert.AreEqual(0, await context.Sessions.CountAsync());
        Assert.AreEqual(usesOlderHash, originalHash != savedHash);
        Assert.AreEqual(PasswordVerificationResult.Success,
            hasher.VerifyHashedPassword(user, savedHash, Password));
    }

    private static User CreateUser()
    {
        return new User
        {
            UserName = "Player", Email = "player@example.test", State = "Active",
            RegistrationDate = DateTime.UtcNow
        };
    }

    private static async Task SeedAsync(AtariGoDbContext context, User user)
    {
        user.RoleId = await context.Roles.Select(role => role.RoleId).SingleAsync();
        context.Users.Add(user);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }

    private static AuthenticationService CreateService(AtariGoDbContext context)
    {
        return LoginTestServices.Create(context);
    }
}
