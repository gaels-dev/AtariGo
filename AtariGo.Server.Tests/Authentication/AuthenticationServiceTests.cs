using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using AtariGo.Server.Application.Authentication;
using AtariGo.Server.Application.Email;
using AtariGo.Server.Application.PasswordRecovery;
using AtariGo.Server.Data;
using AtariGo.Server.Data.Entities;
using AtariGo.Server.Security;
using AtariGo.Server.Tests.Games;
using AtariGo.Server.Tests.PasswordRecovery;

namespace AtariGo.Server.Tests.Authentication;

[TestClass]
[TestCategory("Integration")]
public sealed class AuthenticationServiceTests
{
    private const string Password = "OriginalSecret8";

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task LoginAsync_StoredCredentialAsPassword_RejectsWithoutCreatingSessionAsync(
        bool storesPlaintext)
    {
        await using SqlServerTestDatabase database = await SqlServerTestDatabase.CreateAsync();
        await using AtariGoDbContext context = database.CreateContext();
        User user = CreateUser();
        string stored = storesPlaintext ? Password : new PasswordHasher<User>()
            .HashPassword(user, Password);
        user.Credential = new Credential { PasswordHash = stored };
        await SeedAsync(context, user);
        using var pending = new PendingLoginStore(TimeProvider.System,
            Options.Create(new LoginVerificationOptions()));
        AuthenticationService service = CreateService(context, pending);
        LoginOperationResult result = await service.LoginAsync(new LoginCommand
        {
            Identifier = user.UserName,
            Password = stored
        },
            CancellationToken.None);
        Assert.AreEqual(LoginStatus.InvalidCredentials, result.Status);
        Assert.AreEqual(string.Empty, result.SessionToken);
        Assert.AreEqual(0, await context.Sessions.CountAsync());
        Assert.AreEqual(stored, await context.Credentials.AsNoTracking()
            .Select(credential => credential.PasswordHash).SingleAsync());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task LoginAsync_VerifiedPassword_RequiresCodeAndRefreshesHashWhenNeededAsync(
        bool usesOlderHash)
    {
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
        using var pending = new PendingLoginStore(TimeProvider.System,
            Options.Create(new LoginVerificationOptions()));
        AuthenticationService service = CreateService(context, pending);
        LoginOperationResult result = await service.LoginAsync(new LoginCommand
        {
            Identifier = user.Email,
            Password = Password
        },
            CancellationToken.None);
        string savedHash = await context.Credentials.AsNoTracking()
            .Select(credential => credential.PasswordHash).SingleAsync();
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
            UserName = "Player",
            Email = "player@example.test",
            State = "Active",
            RegistrationDate = DateTime.UtcNow
        };
    }

    private static AuthenticationService CreateService(AtariGoDbContext context,
        PendingLoginStore pending)
    {
        TimeProvider time = TimeProvider.System;
        var attempts = new LoginAttemptService(context, time);
        IOptions<PasswordRecoveryOptions> recovery = Options.Create(new PasswordRecoveryOptions
        {
            HmacKey = new string('K', 64)
        });
        IOptions<LoginVerificationOptions> login = Options.Create(new LoginVerificationOptions());
        var protector = new AuthenticationCodeProtector(recovery.Value.HmacKey);
        var issuance = new AuthenticationCodeIssuanceService(context, login, recovery, time);
        var email = new RecordingEmailSender { ExpectedRecipient = "player@example.test" };
        var delivery = new LoginCodeDeliveryService(context, new EmailDeliveryService(email),
            protector, issuance, time);
        var challenges = new LoginChallengeService(context, delivery, attempts,
            new SessionTokenProvider(), pending);
        return new AuthenticationService(context, new PasswordHasher<User>(), attempts, challenges);
    }

    private static async Task SeedAsync(AtariGoDbContext context, User user)
    {
        context.Users.Add(user);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }
}
