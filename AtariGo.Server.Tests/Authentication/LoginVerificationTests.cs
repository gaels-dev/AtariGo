using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using AtariGo.Contracts;
using AtariGo.Server.Application.Authentication;
using AtariGo.Server.Application.Credentials;
using AtariGo.Server.Data;
using AtariGo.Server.Data.Entities;
using AtariGo.Server.Tests.PasswordRecovery;

namespace AtariGo.Server.Tests.Authentication;

[TestClass]
[TestCategory("Integration")]
[TestCategory("SqlServer")]
public sealed class LoginVerificationTests
{
    [TestMethod]
    public async Task Login_CustomResendDelay_ReturnsConfiguredDeadline()
    {
        // Arrange
        await using RecoveryTestHost host = await RecoveryTestHost.CreateAsync();
        host.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<
            LoginVerificationOptions>>().Value.ResendSeconds = 120;
        // Act
        LoginResponse pending = await LoginAsync(host);
        host.Clock.Advance(TimeSpan.FromSeconds(60));
        LoginResponse early = await ResendAsync(host, pending.ChallengeToken);
        // Assert
        Assert.AreEqual(host.Clock.GetUtcNow().AddSeconds(60).ToUnixTimeSeconds(),
            pending.RetryAtUnixSeconds);
        Assert.AreEqual(LoginResult.RateLimited, early.Result);
        Assert.AreEqual(pending.RetryAtUnixSeconds, early.RetryAtUnixSeconds);
        Assert.AreEqual(1, host.Email.Codes.Count);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task Login_MissingEmailConfiguration_DeniesAccessWithoutPenalizingPlayer(
        bool missingSmtp)
    {
        // Arrange
        await using RecoveryTestHost host = await RecoveryTestHost.CreateAsync();
        if (missingSmtp)
        {
            host.Email.IsConfigured = false;
        }
        else
        {
            host.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<
                AtariGo.Server.Application.PasswordRecovery.PasswordRecoveryOptions>>()
                .Value.HmacKey = string.Empty;
        }
        // Act
        LoginResponse result = await LoginAsync(host);
        // Assert
        Assert.AreEqual(LoginResult.Unavailable, result.Result);
        Assert.AreEqual(string.Empty, result.SessionToken);
        await using AtariGoDbContext context = host.Database.CreateContext();
        Assert.AreEqual(0, await context.LoginChallenges.CountAsync());
        Assert.AreEqual(0, await context.Credentials.Where(item => item.UserId == 1)
            .Select(item => item.FailedAttempts).SingleAsync());
    }

    [TestMethod]
    public async Task ResendLoginCode_ConcurrentConfirmation_ProducesOnlyOneAuthorizedSession()
    {
        // Arrange
        await using RecoveryTestHost host = await RecoveryTestHost.CreateAsync();
        LoginResponse pending = await LoginAsync(host);
        string previous = host.Email.Codes.Last();
        host.Clock.Advance(TimeSpan.FromSeconds(60));
        await using AtariGoDbContext context = host.Database.CreateContext();
        int before = await context.Sessions.CountAsync();
        // Act
        Task<LoginResponse> resend = ResendAsync(host, pending.ChallengeToken);
        Task<LoginResponse> verify = VerifyAsync(host, pending.ChallengeToken, previous);
        await Task.WhenAll(resend, verify);
        if ((await verify).Result != LoginResult.Success)
        {
            LoginResponse final = await VerifyAsync(host, pending.ChallengeToken,
                host.Email.Codes.Last());
            Assert.AreEqual(LoginResult.Success, final.Result);
        }
        // Assert
        Assert.AreEqual(before + 1, await context.Sessions.CountAsync());
        Assert.IsTrue((await resend).Result is LoginResult.ChallengeRequired
            or LoginResult.InvalidChallenge);
    }

    [TestMethod]
    public async Task Login_CorrectPassword_RequiresCodeAndConsumesItExactlyOnce()
    {
        // Arrange
        await using RecoveryTestHost host = await RecoveryTestHost.CreateAsync();
        // Act
        LoginResponse pending = await LoginAsync(host);
        await using AtariGoDbContext context = host.Database.CreateContext();
        int sessionsBefore = await context.Sessions.CountAsync();
        LoginResponse[] replies = await Task.WhenAll(
            VerifyAsync(host, pending.ChallengeToken, host.Email.Codes.Last()),
            VerifyAsync(host, pending.ChallengeToken, host.Email.Codes.Last()));
        // Assert
        Assert.AreEqual(LoginResult.ChallengeRequired, pending.Result);
        Assert.AreEqual(string.Empty, pending.SessionToken);
        Assert.AreEqual("O***@example.test", pending.MaskedEmail);
        Assert.AreEqual(1, replies.Count(reply => reply.Result == LoginResult.Success));
        Assert.AreEqual(1, replies.Count(reply => reply.Result == LoginResult.InvalidChallenge));
        Assert.AreEqual(sessionsBefore + 1, await context.Sessions.CountAsync());
        LoginChallenge saved = await context.LoginChallenges.AsNoTracking().SingleAsync();
        Assert.AreNotEqual(pending.ChallengeToken, saved.TokenHash);
        Assert.IsNotNull(saved.ClosedAt);
        Assert.AreEqual(string.Empty, host.Email.LastMessage?.Body);
    }

    [TestMethod]
    public async Task Login_UnknownAccountAndWrongPassword_UseSamePublicResponse()
    {
        // Arrange
        await using RecoveryTestHost host = await RecoveryTestHost.CreateAsync();
        // Act
        LoginResponse unknown = await LoginAsync(host, "Missing", "Wrong8");
        LoginResponse wrong = await LoginAsync(host, "Owner", "Wrong8");
        // Assert
        Assert.AreEqual(LoginResult.InvalidCredentials, unknown.Result);
        Assert.AreEqual(unknown.ToString(), wrong.ToString());
        Assert.AreEqual(0, host.Email.Codes.Count);
        await using AtariGoDbContext context = host.Database.CreateContext();
        Assert.AreEqual(1, await context.Credentials.Where(item => item.UserId == 1)
            .Select(item => item.FailedAttempts).SingleAsync());
    }

    [TestMethod]
    public async Task VerifyLoginCode_CombinedFailures_LocksForFifteenMinutesAndResetsAfterExpiry()
    {
        // Arrange
        await using RecoveryTestHost host = await RecoveryTestHost.CreateAsync();
        await LoginAsync(host, "Owner", "Wrong8");
        await LoginAsync(host, "Owner", "Wrong8");
        LoginResponse pending = await LoginAsync(host);
        string wrong = host.Email.Codes.Last() == "00000000" ? "11111111" : "00000000";
        // Act
        LoginResponse blocked = await VerifyAsync(host, pending.ChallengeToken, wrong);
        LoginResponse denied = await LoginAsync(host);
        host.Clock.Advance(TimeSpan.FromMinutes(15));
        LoginResponse next = await LoginAsync(host);
        LoginResponse confirmed = await VerifyAsync(host, next.ChallengeToken,
            host.Email.Codes.Last());
        // Assert
        Assert.AreEqual(LoginResult.TemporarilyLocked, blocked.Result);
        Assert.AreEqual(LoginResult.TemporarilyLocked, denied.Result);
        Assert.AreEqual(host.Clock.GetUtcNow().ToUnixTimeSeconds(), blocked.RetryAtUnixSeconds);
        Assert.AreEqual(LoginResult.Success, confirmed.Result);
        await using AtariGoDbContext context = host.Database.CreateContext();
        Assert.AreEqual(0, await context.Credentials.Where(item => item.UserId == 1)
            .Select(item => item.FailedAttempts).SingleAsync());
    }

    [TestMethod]
    public async Task VerifyLoginCode_ExpiredAndResentCodes_PreserveAttemptsAndReplacePreviousCode()
    {
        // Arrange
        await using RecoveryTestHost host = await RecoveryTestHost.CreateAsync();
        LoginResponse pending = await LoginAsync(host);
        string previous = host.Email.Codes.Last();
        host.Clock.Advance(TimeSpan.FromMinutes(10));
        // Act
        LoginResponse expired = await VerifyAsync(host, pending.ChallengeToken, previous);
        LoginResponse resent = await ResendAsync(host, pending.ChallengeToken);
        LoginResponse previousReply = await VerifyAsync(host, pending.ChallengeToken, previous);
        LoginResponse current = await VerifyAsync(host, pending.ChallengeToken,
            host.Email.Codes.Last());
        // Assert
        Assert.AreEqual(LoginResult.CodeExpired, expired.Result);
        Assert.AreEqual(LoginResult.ChallengeRequired, resent.Result);
        Assert.AreEqual(LoginResult.InvalidCode, previousReply.Result);
        Assert.AreEqual(LoginResult.Success, current.Result);
        await using AtariGoDbContext context = host.Database.CreateContext();
        Assert.AreEqual(1, await context.AuthenticationCodes.Where(code => code.UserId == 1)
            .SumAsync(code => code.AttemptCount));
    }

    [TestMethod]
    public async Task ResendLoginCode_DeliveryFailsTwice_ClosesAttemptWithoutPenalizingPlayer()
    {
        // Arrange
        await using RecoveryTestHost host = await RecoveryTestHost.CreateAsync();
        host.Email.FailDelivery = true;
        // Act
        LoginResponse first = await LoginAsync(host);
        host.Clock.Advance(TimeSpan.FromSeconds(60));
        LoginResponse second = await ResendAsync(host, first.ChallengeToken);
        // Assert
        Assert.AreEqual(LoginResult.DeliveryFailed, first.Result);
        Assert.IsFalse(string.IsNullOrWhiteSpace(first.ChallengeToken));
        Assert.AreEqual(LoginResult.DeliveryFailed, second.Result);
        Assert.AreEqual(string.Empty, second.ChallengeToken);
        await using AtariGoDbContext context = host.Database.CreateContext();
        Assert.AreEqual(0, await context.Credentials.Where(item => item.UserId == 1)
            .Select(item => item.FailedAttempts).SingleAsync());
        Assert.IsNotNull((await context.LoginChallenges.SingleAsync()).ClosedAt);
    }

    [TestMethod]
    public async Task VerifyLoginCode_SessionWriteFails_RollsBackConsumptionAndAllowsManualRetry()
    {
        // Arrange
        await using RecoveryTestHost host = await RecoveryTestHost.CreateAsync();
        LoginResponse pending = await LoginAsync(host);
        await using AtariGoDbContext context = host.Database.CreateContext();
        await context.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER dbo.FailLoginSession ON dbo.Sessions AFTER INSERT AS
            THROW 51000, 'Simulated login session failure.', 1;
            """);
        // Act
        await Assert.ThrowsAsync<Grpc.Core.RpcException>(() => VerifyAsync(host,
            pending.ChallengeToken, host.Email.Codes.Last()));
        LoginChallenge saved = await context.LoginChallenges.AsNoTracking().SingleAsync();
        await context.Database.ExecuteSqlRawAsync("DROP TRIGGER dbo.FailLoginSession;");
        LoginResponse retry = await VerifyAsync(host, pending.ChallengeToken,
            host.Email.Codes.Last());
        // Assert
        Assert.IsNull(saved.ClosedAt);
        Assert.AreEqual(LoginResult.Success, retry.Result);
        Assert.AreEqual(0, await context.Credentials.Where(item => item.UserId == 1)
            .Select(item => item.FailedAttempts).SingleAsync());
    }

    [TestMethod]
    public async Task Login_ActiveSuspension_ShowsReasonAndDeadlineWithoutSendingCode()
    {
        // Arrange
        await using RecoveryTestHost host = await RecoveryTestHost.CreateAsync();
        await using AtariGoDbContext context = host.Database.CreateContext();
        DateTime now = host.Clock.GetUtcNow().UtcDateTime;
        context.Sanctions.Add(new Sanction
        {
            UserId = 1, AdministratorId = 2, SanctionType = "TemporarySuspension",
            Reason = "Test suspension", StartedAt = now, EndsAt = now.AddDays(1), State = "Active"
        });
        await context.SaveChangesAsync();
        // Act
        LoginResponse result = await LoginAsync(host);
        // Assert
        Assert.AreEqual(LoginResult.AccountRestricted, result.Result);
        Assert.AreEqual("Test suspension", result.RestrictionReason);
        Assert.AreEqual(0, host.Email.Codes.Count);
        Assert.IsGreaterThan(0, result.RetryAtUnixSeconds);
    }

    [TestMethod]
    public async Task VerifyLoginCode_AccountSuspendedAfterIssuance_DoesNotCreateSession()
    {
        // Arrange
        await using RecoveryTestHost host = await RecoveryTestHost.CreateAsync();
        LoginResponse pending = await LoginAsync(host);
        await using AtariGoDbContext context = host.Database.CreateContext();
        await context.Users.Where(user => user.UserId == 1)
            .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.State, "Suspended"));
        // Act
        LoginResponse result = await VerifyAsync(host, pending.ChallengeToken,
            host.Email.Codes.Last());
        // Assert
        Assert.AreEqual(LoginResult.AccountRestricted, result.Result);
        Assert.AreEqual(string.Empty, result.SessionToken);
    }

    [TestMethod]
    public async Task VerifyLoginCode_PasswordChanged_InvalidatesPendingLogin()
    {
        // Arrange
        await using RecoveryTestHost host = await RecoveryTestHost.CreateAsync();
        LoginResponse pending = await LoginAsync(host);
        using IServiceScope scope = host.Services.CreateScope();
        AtariGoDbContext context = scope.ServiceProvider.GetRequiredService<AtariGoDbContext>();
        var credentials = new CredentialsService(context,
            new Microsoft.AspNetCore.Identity.PasswordHasher<User>(), host.Clock);
        // Act
        ChangePasswordResult changed = await credentials.ChangePasswordAsync(
            new AtariGo.Server.Application.Sessions.SessionIdentity(1, "Owner"),
            new ChangePasswordCommand
            {
                CurrentPassword = RecoveryTestHost.OriginalPassword,
                NewPassword = RecoveryTestHost.NewPassword,
                Confirmation = RecoveryTestHost.NewPassword
            }, CancellationToken.None);
        LoginResponse result = await VerifyAsync(host, pending.ChallengeToken,
            host.Email.Codes.Last());
        // Assert
        Assert.AreEqual(ChangePasswordResult.Changed, changed);
        Assert.AreEqual(LoginResult.InvalidChallenge, result.Result);
    }

    [TestMethod]
    public async Task VerifyLoginCode_PasswordRecovered_InvalidatesPendingLogin()
    {
        // Arrange
        await using RecoveryTestHost host = await RecoveryTestHost.CreateAsync();
        LoginResponse pending = await LoginAsync(host);
        string loginCode = host.Email.Codes.Last();
        await host.RequestAsync();
        // Act
        PasswordRecoveryResponse recovered = await host.ResetAsync(host.CreateReset());
        LoginResponse result = await VerifyAsync(host, pending.ChallengeToken, loginCode);
        // Assert
        Assert.AreEqual(RecoveryStatus.Reset, recovered.Status);
        Assert.AreEqual(LoginResult.InvalidChallenge, result.Result);
    }

    [TestMethod]
    public async Task CancelLogin_ValidAttempt_InvalidatesCodeAndDoesNotCountFailure()
    {
        // Arrange
        await using RecoveryTestHost host = await RecoveryTestHost.CreateAsync();
        LoginResponse pending = await LoginAsync(host);
        var client = new Contracts.Authentication.AuthenticationClient(host.Channel);
        // Act
        await client.CancelLoginAsync(new CancelLoginRequest
        {
            ChallengeToken = pending.ChallengeToken
        });
        LoginResponse result = await VerifyAsync(host, pending.ChallengeToken,
            host.Email.Codes.Last());
        // Assert
        Assert.AreEqual(LoginResult.InvalidChallenge, result.Result);
        await using AtariGoDbContext context = host.Database.CreateContext();
        Assert.AreEqual(0, await context.Credentials.Where(item => item.UserId == 1)
            .Select(item => item.FailedAttempts).SingleAsync());
    }

    [TestMethod]
    public async Task ResendLoginCode_TooSoonOrAfterIssuanceWindow_DoesNotSendEmail()
    {
        // Arrange
        await using RecoveryTestHost host = await RecoveryTestHost.CreateAsync();
        LoginResponse pending = await LoginAsync(host);
        // Act
        LoginResponse early = await ResendAsync(host, pending.ChallengeToken);
        LoginResponse freshLogin = await LoginAsync(host);
        host.Clock.Advance(TimeSpan.FromMinutes(30));
        LoginResponse late = await ResendAsync(host, pending.ChallengeToken);
        // Assert
        Assert.AreEqual(LoginResult.RateLimited, early.Result);
        Assert.AreEqual(LoginResult.RateLimited, freshLogin.Result);
        Assert.AreEqual(LoginResult.InvalidChallenge, late.Result);
        Assert.AreEqual(1, host.Email.Codes.Count);
    }

    private static Task<LoginResponse> LoginAsync(RecoveryTestHost host,
        string identifier = "Owner", string password = RecoveryTestHost.OriginalPassword)
    {
        var client = new Contracts.Authentication.AuthenticationClient(host.Channel);
        return client.LoginAsync(new LoginRequest
        {
            Identifier = identifier, Password = password, CultureName = "es-MX"
        }, deadline: DateTime.UtcNow.AddSeconds(30)).ResponseAsync;
    }

    private static Task<LoginResponse> VerifyAsync(RecoveryTestHost host, string token, string code)
    {
        var client = new Contracts.Authentication.AuthenticationClient(host.Channel);
        return client.VerifyLoginCodeAsync(new VerifyLoginCodeRequest
        {
            ChallengeToken = token, Code = code
        }, deadline: DateTime.UtcNow.AddSeconds(30)).ResponseAsync;
    }

    private static Task<LoginResponse> ResendAsync(RecoveryTestHost host, string token)
    {
        var client = new Contracts.Authentication.AuthenticationClient(host.Channel);
        return client.ResendLoginCodeAsync(new ResendLoginCodeRequest
        {
            ChallengeToken = token, CultureName = "es-MX"
        }, deadline: DateTime.UtcNow.AddSeconds(30)).ResponseAsync;
    }
}
