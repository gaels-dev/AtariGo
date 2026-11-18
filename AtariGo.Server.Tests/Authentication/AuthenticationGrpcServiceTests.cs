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
public sealed class AuthenticationGrpcServiceTests
{
    [TestMethod]
    public async Task Login_ExistingSchema_CompletesWithoutAddingTablesOrColumnsAsync()
    {
        await using RecoveryTestHost host = await RecoveryTestHost.CreateAsync();
        await using AtariGoDbContext context = host.Database.CreateContext();
        LoginResponse pending = await LoginAsync(host);
        LoginResponse confirmed = await VerifyAsync(host, pending.ChallengeToken,
            host.Email.Codes.Last());
        Assert.AreEqual(LoginResult.Success, confirmed.Result);
        Assert.AreEqual(17, await context.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS Value FROM sys.tables").SingleAsync());
        Assert.AreEqual(5, await context.Database.SqlQueryRaw<int>("""
            SELECT COUNT(*) AS Value FROM sys.columns
            WHERE object_id = OBJECT_ID('dbo.Credentials')
            """).SingleAsync());
        Assert.AreEqual(8, await context.Database.SqlQueryRaw<int>("""
            SELECT COUNT(*) AS Value FROM sys.columns
            WHERE object_id = OBJECT_ID('dbo.AuthenticationCodes')
            """).SingleAsync());
    }

    [TestMethod]
    public async Task VerifyLoginCode_ServerRestart_RequiresNewCredentialsAndNewCodeAsync()
    {
        await using RecoveryTestHost host = await RecoveryTestHost.CreateAsync();
        LoginResponse previous = await LoginAsync(host);
        string previousCode = host.Email.Codes.Last();
        await host.RestartAsync();
        LoginResponse stale = await VerifyAsync(host, previous.ChallengeToken, previousCode);
        LoginResponse resend = await ResendAsync(host, previous.ChallengeToken);
        host.Clock.Advance(TimeSpan.FromSeconds(60));
        LoginResponse current = await LoginAsync(host);
        LoginResponse confirmed = await VerifyAsync(host, current.ChallengeToken,
            host.Email.Codes.Last());
        Assert.AreEqual(LoginResult.InvalidChallenge, stale.Result);
        Assert.AreEqual(LoginResult.InvalidChallenge, resend.Result);
        Assert.AreEqual(LoginResult.Success, confirmed.Result);
    }

    [TestMethod]
    public async Task VerifyLoginCode_CodeFromAnotherAttempt_DoesNotAuthorizeAccessAsync()
    {
        await using RecoveryTestHost host = await RecoveryTestHost.CreateAsync();
        LoginResponse first = await LoginAsync(host);
        string firstCode = host.Email.Codes.Last();
        host.Clock.Advance(TimeSpan.FromSeconds(60));
        LoginResponse second = await LoginAsync(host);
        string secondCode = host.Email.Codes.Last();
        LoginResponse crossed = await VerifyAsync(host, second.ChallengeToken, firstCode);
        LoginResponse confirmed = await VerifyAsync(host, second.ChallengeToken, secondCode);
        Assert.AreEqual(LoginResult.InvalidCode, crossed.Result);
        Assert.AreEqual(LoginResult.Success, confirmed.Result);
        Assert.AreEqual(string.Empty, crossed.SessionToken);
        Assert.AreNotEqual(first.ChallengeToken, second.ChallengeToken);
    }

    [TestMethod]
    public async Task ResendLoginCode_PreviousDeliveryFailsLate_DoesNotInvalidateNewerCodeAsync()
    {
        await using RecoveryTestHost host = await RecoveryTestHost.CreateAsync();
        LoginResponse pending = await LoginAsync(host);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        host.Email.FailDelivery = true;
        host.Email.SendStarted = started;
        host.Email.SendCompletion = release.Task;
        host.Clock.Advance(TimeSpan.FromSeconds(60));
        Task<LoginResponse> previous = ResendAsync(host, pending.ChallengeToken);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        LoginResponse current;
        try
        {
            host.Email.SendCompletion = null;
            host.Email.FailDelivery = false;
            host.Clock.Advance(TimeSpan.FromSeconds(60));
            current = await ResendAsync(host, pending.ChallengeToken);
        }
        finally
        {
            release.TrySetResult();
        }
        LoginResponse late = await previous;
        LoginResponse confirmed = await VerifyAsync(host, pending.ChallengeToken,
            host.Email.Codes.Last());
        Assert.AreEqual(LoginResult.ChallengeRequired, current.Result);
        Assert.AreEqual(LoginResult.InvalidChallenge, late.Result);
        Assert.AreEqual(LoginResult.Success, confirmed.Result);
    }

    [TestMethod]
    public async Task ResendLoginCode_CancelledDuringDelivery_CannotRestoreCancelledAttemptAsync()
    {
        await using RecoveryTestHost host = await RecoveryTestHost.CreateAsync();
        LoginResponse pending = await LoginAsync(host);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        host.Email.SendStarted = started;
        host.Email.SendCompletion = release.Task;
        host.Clock.Advance(TimeSpan.FromSeconds(60));
        Task<LoginResponse> resend = ResendAsync(host, pending.ChallengeToken);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            var client = new Contracts.Authentication.AuthenticationClient(host.Channel);
            await client.CancelLoginAsync(new CancelLoginRequest
            {
                ChallengeToken = pending.ChallengeToken
            });
        }
        finally
        {
            release.TrySetResult();
        }
        LoginResponse late = await resend;
        LoginResponse confirmed = await VerifyAsync(host, pending.ChallengeToken,
            host.Email.Codes.Last());
        Assert.AreEqual(LoginResult.InvalidChallenge, late.Result);
        Assert.AreEqual(LoginResult.InvalidChallenge, confirmed.Result);
    }

    [TestMethod]
    public async Task ResendLoginCode_CodeWriteFails_PreservesPreviousCodeAndPendingStateAsync()
    {
        await using RecoveryTestHost host = await RecoveryTestHost.CreateAsync();
        LoginResponse pending = await LoginAsync(host);
        string code = host.Email.Codes.Last();
        host.Clock.Advance(TimeSpan.FromSeconds(60));
        await using AtariGoDbContext context = host.Database.CreateContext();
        await context.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER dbo.FailLoginCode ON dbo.AuthenticationCodes AFTER INSERT AS
            THROW 51000, 'Simulated code write failure.', 1;
            """);
        await Assert.ThrowsAsync<Grpc.Core.RpcException>(() => ResendAsync(host,
            pending.ChallengeToken));
        await context.Database.ExecuteSqlRawAsync("DROP TRIGGER dbo.FailLoginCode;");
        LoginResponse confirmed = await VerifyAsync(host, pending.ChallengeToken, code);
        Assert.AreEqual(LoginResult.Success, confirmed.Result);
        Assert.AreEqual(1, await context.AuthenticationCodes.CountAsync());
        Assert.AreEqual(1, host.Email.Codes.Count);
    }

    [TestMethod]
    public async Task ResendLoginCode_PasswordRecoveredAfterMailFailure_RejectsOldAttemptAsync()
    {
        await using RecoveryTestHost host = await RecoveryTestHost.CreateAsync();
        host.Email.FailDelivery = true;
        LoginResponse pending = await LoginAsync(host);
        string undelivered = host.Email.LastAttemptedCode;
        LoginResponse denied = await VerifyAsync(host, pending.ChallengeToken, undelivered);
        host.Email.FailDelivery = false;
        await host.RequestAsync();
        PasswordRecoveryResponse recovered = await host.ResetAsync(host.CreateReset());
        host.Clock.Advance(TimeSpan.FromSeconds(60));
        LoginResponse resent = await ResendAsync(host, pending.ChallengeToken);
        Assert.AreEqual(LoginResult.DeliveryFailed, pending.Result);
        Assert.AreEqual(LoginResult.InvalidChallenge, denied.Result);
        Assert.AreEqual(RecoveryStatus.Reset, recovered.Status);
        Assert.AreEqual(LoginResult.InvalidChallenge, resent.Result);
    }

    [TestMethod]
    public async Task
        VerifyLoginCode_LastEmissionNearIssuanceDeadline_RetainsTenMinuteLifetimeAsync()
    {
        await using RecoveryTestHost host = await RecoveryTestHost.CreateAsync();
        LoginResponse pending = await LoginAsync(host);
        host.Clock.Advance(TimeSpan.FromMinutes(29));
        LoginResponse resent = await ResendAsync(host, pending.ChallengeToken);
        host.Clock.Advance(TimeSpan.FromMinutes(9));
        LoginResponse confirmed = await VerifyAsync(host, pending.ChallengeToken,
            host.Email.Codes.Last());
        Assert.AreEqual(LoginResult.ChallengeRequired, resent.Result);
        Assert.AreEqual(LoginResult.Success, confirmed.Result);
    }

    [TestMethod]
    public async Task Login_PendingStoreAtCapacity_DeniesNewAttemptAndPreservesExistingOneAsync()
    {
        await using RecoveryTestHost host = await RecoveryTestHost.CreateAsync();
        host.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<
            LoginVerificationOptions>>().Value.MaximumPendingLogins = 1;
        LoginResponse pending = await LoginAsync(host);
        string code = host.Email.Codes.Last();
        host.Clock.Advance(TimeSpan.FromSeconds(60));
        LoginResponse full = await LoginAsync(host);
        LoginResponse confirmed = await VerifyAsync(host, pending.ChallengeToken, code);
        host.Clock.Advance(TimeSpan.FromSeconds(60));
        LoginResponse next = await LoginAsync(host);
        Assert.AreEqual(LoginResult.Unavailable, full.Result);
        Assert.AreEqual(LoginResult.Success, confirmed.Result);
        Assert.AreEqual(LoginResult.ChallengeRequired, next.Result);
        Assert.AreEqual(2, host.Email.Codes.Count);
    }

    [TestMethod]
    public async Task Login_CustomResendDelay_ReturnsConfiguredDeadlineAsync()
    {
        await using RecoveryTestHost host = await RecoveryTestHost.CreateAsync();
        host.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<
            LoginVerificationOptions>>().Value.ResendSeconds = 120;
        LoginResponse pending = await LoginAsync(host);
        host.Clock.Advance(TimeSpan.FromSeconds(60));
        LoginResponse early = await ResendAsync(host, pending.ChallengeToken);
        Assert.AreEqual(host.Clock.GetUtcNow().AddSeconds(60).ToUnixTimeSeconds(),
            pending.RetryAtUnixSeconds);
        Assert.AreEqual(LoginResult.RateLimited, early.Result);
        Assert.AreEqual(pending.RetryAtUnixSeconds, early.RetryAtUnixSeconds);
        Assert.AreEqual(1, host.Email.Codes.Count);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task Login_MissingEmailConfiguration_DeniesAccessWithoutPenalizingPlayerAsync(
        bool missingSmtp)
    {
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
        LoginResponse result = await LoginAsync(host);
        Assert.AreEqual(LoginResult.Unavailable, result.Result);
        Assert.AreEqual(string.Empty, result.SessionToken);
        await using AtariGoDbContext context = host.Database.CreateContext();
        Assert.AreEqual(0, await context.AuthenticationCodes.CountAsync());
        Assert.AreEqual(0, await context.Credentials.Where(item => item.UserId == 1)
            .Select(item => item.FailedAttempts).SingleAsync());
    }

    [TestMethod]
    public async Task ResendLoginCode_ConcurrentConfirmation_ProducesOnlyOneAuthorizedSessionAsync()
    {
        await using RecoveryTestHost host = await RecoveryTestHost.CreateAsync();
        LoginResponse pending = await LoginAsync(host);
        string previous = host.Email.Codes.Last();
        host.Clock.Advance(TimeSpan.FromSeconds(60));
        await using AtariGoDbContext context = host.Database.CreateContext();
        int before = await context.Sessions.CountAsync();
        Task<LoginResponse> resend = ResendAsync(host, pending.ChallengeToken);
        Task<LoginResponse> verify = VerifyAsync(host, pending.ChallengeToken, previous);
        await Task.WhenAll(resend, verify);
        if ((await verify).Result != LoginResult.Success)
        {
            LoginResponse final = await VerifyAsync(host, pending.ChallengeToken,
                host.Email.Codes.Last());
            Assert.AreEqual(LoginResult.Success, final.Result);
        }
        Assert.AreEqual(before + 1, await context.Sessions.CountAsync());
        Assert.IsTrue((await resend).Result is LoginResult.ChallengeRequired
            or LoginResult.InvalidChallenge);
    }

    [TestMethod]
    public async Task Login_CorrectPassword_RequiresCodeAndConsumesItExactlyOnceAsync()
    {
        await using RecoveryTestHost host = await RecoveryTestHost.CreateAsync();
        LoginResponse pending = await LoginAsync(host);
        await using AtariGoDbContext context = host.Database.CreateContext();
        int sessionsBefore = await context.Sessions.CountAsync();
        LoginResponse[] replies = await Task.WhenAll(
            VerifyAsync(host, pending.ChallengeToken, host.Email.Codes.Last()),
            VerifyAsync(host, pending.ChallengeToken, host.Email.Codes.Last()));
        Assert.AreEqual(LoginResult.ChallengeRequired, pending.Result);
        Assert.AreEqual(string.Empty, pending.SessionToken);
        Assert.AreEqual("O***@example.test", pending.MaskedEmail);
        Assert.AreEqual(1, replies.Count(reply => reply.Result == LoginResult.Success));
        Assert.AreEqual(1, replies.Count(reply => reply.Result == LoginResult.InvalidChallenge));
        Assert.AreEqual(sessionsBefore + 1, await context.Sessions.CountAsync());
        AuthenticationCode saved = await context.AuthenticationCodes.AsNoTracking().SingleAsync();
        Assert.IsNotNull(saved.UsedAt);
        Assert.AreNotEqual(host.Email.Codes.Last(), saved.CodeHash);
        Assert.AreEqual(string.Empty, host.Email.LastMessage?.Body);
    }

    [TestMethod]
    public async Task Login_UnknownAccountAndWrongPassword_UseSamePublicResponseAsync()
    {
        await using RecoveryTestHost host = await RecoveryTestHost.CreateAsync();
        LoginResponse unknown = await LoginAsync(host, "Missing", "Wrong8");
        LoginResponse wrong = await LoginAsync(host, "Owner", "Wrong8");
        Assert.AreEqual(LoginResult.InvalidCredentials, unknown.Result);
        Assert.AreEqual(unknown.ToString(), wrong.ToString());
        Assert.AreEqual(0, host.Email.Codes.Count);
        await using AtariGoDbContext context = host.Database.CreateContext();
        Assert.AreEqual(1, await context.Credentials.Where(item => item.UserId == 1)
            .Select(item => item.FailedAttempts).SingleAsync());
    }

    [TestMethod]
    public async Task
        VerifyLoginCode_CombinedFailures_LocksForFifteenMinutesAndResetsAfterExpiryAsync()
    {
        await using RecoveryTestHost host = await RecoveryTestHost.CreateAsync();
        await LoginAsync(host, "Owner", "Wrong8");
        await LoginAsync(host, "Owner", "Wrong8");
        LoginResponse pending = await LoginAsync(host);
        string wrong = host.Email.Codes.Last() == "00000000" ? "11111111" : "00000000";
        LoginResponse blocked = await VerifyAsync(host, pending.ChallengeToken, wrong);
        LoginResponse denied = await LoginAsync(host);
        host.Clock.Advance(TimeSpan.FromMinutes(15));
        LoginResponse next = await LoginAsync(host);
        LoginResponse confirmed = await VerifyAsync(host, next.ChallengeToken,
            host.Email.Codes.Last());
        Assert.AreEqual(LoginResult.TemporarilyLocked, blocked.Result);
        Assert.AreEqual(LoginResult.TemporarilyLocked, denied.Result);
        Assert.AreEqual(host.Clock.GetUtcNow().ToUnixTimeSeconds(), blocked.RetryAtUnixSeconds);
        Assert.AreEqual(LoginResult.Success, confirmed.Result);
        await using AtariGoDbContext context = host.Database.CreateContext();
        Assert.AreEqual(0, await context.Credentials.Where(item => item.UserId == 1)
            .Select(item => item.FailedAttempts).SingleAsync());
    }

    [TestMethod]
    public async Task
        VerifyLoginCode_ExpiredAndResentCodes_PreserveAttemptsAndReplacePreviousCodeAsync()
    {
        await using RecoveryTestHost host = await RecoveryTestHost.CreateAsync();
        LoginResponse pending = await LoginAsync(host);
        string previous = host.Email.Codes.Last();
        host.Clock.Advance(TimeSpan.FromMinutes(10));
        LoginResponse expired = await VerifyAsync(host, pending.ChallengeToken, previous);
        LoginResponse resent = await ResendAsync(host, pending.ChallengeToken);
        LoginResponse previousReply = await VerifyAsync(host, pending.ChallengeToken, previous);
        LoginResponse current = await VerifyAsync(host, pending.ChallengeToken,
            host.Email.Codes.Last());
        Assert.AreEqual(LoginResult.CodeExpired, expired.Result);
        Assert.AreEqual(LoginResult.ChallengeRequired, resent.Result);
        Assert.AreEqual(LoginResult.InvalidCode, previousReply.Result);
        Assert.AreEqual(LoginResult.Success, current.Result);
        await using AtariGoDbContext context = host.Database.CreateContext();
        Assert.AreEqual(1, await context.AuthenticationCodes.Where(code => code.UserId == 1)
            .SumAsync(code => code.AttemptCount));
    }

    [TestMethod]
    public async Task ResendLoginCode_DeliveryFailsTwice_ClosesAttemptWithoutPenalizingPlayerAsync()
    {
        await using RecoveryTestHost host = await RecoveryTestHost.CreateAsync();
        host.Email.FailDelivery = true;
        LoginResponse first = await LoginAsync(host);
        host.Clock.Advance(TimeSpan.FromSeconds(60));
        LoginResponse second = await ResendAsync(host, first.ChallengeToken);
        Assert.AreEqual(LoginResult.DeliveryFailed, first.Result);
        Assert.IsFalse(string.IsNullOrWhiteSpace(first.ChallengeToken));
        Assert.AreEqual(LoginResult.DeliveryFailed, second.Result);
        Assert.AreEqual(string.Empty, second.ChallengeToken);
        await using AtariGoDbContext context = host.Database.CreateContext();
        Assert.AreEqual(0, await context.Credentials.Where(item => item.UserId == 1)
            .Select(item => item.FailedAttempts).SingleAsync());
        Assert.AreEqual(0, await context.AuthenticationCodes
            .CountAsync(code => code.UsedAt == null));
    }

    [TestMethod]
    public async Task
        VerifyLoginCode_SessionWriteFails_RollsBackConsumptionAndAllowsManualRetryAsync()
    {
        await using RecoveryTestHost host = await RecoveryTestHost.CreateAsync();
        LoginResponse pending = await LoginAsync(host);
        await using AtariGoDbContext context = host.Database.CreateContext();
        await context.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER dbo.FailLoginSession ON dbo.Sessions AFTER INSERT AS
            THROW 51000, 'Simulated login session failure.', 1;
            """);
        await Assert.ThrowsAsync<Grpc.Core.RpcException>(() => VerifyAsync(host,
            pending.ChallengeToken, host.Email.Codes.Last()));
        AuthenticationCode saved = await context.AuthenticationCodes.AsNoTracking().SingleAsync();
        await context.Database.ExecuteSqlRawAsync("DROP TRIGGER dbo.FailLoginSession;");
        LoginResponse retry = await VerifyAsync(host, pending.ChallengeToken,
            host.Email.Codes.Last());
        Assert.IsNull(saved.UsedAt);
        Assert.AreEqual(LoginResult.Success, retry.Result);
        Assert.AreEqual(0, await context.Credentials.Where(item => item.UserId == 1)
            .Select(item => item.FailedAttempts).SingleAsync());
    }

    [TestMethod]
    public async Task Login_ActiveSuspension_ShowsReasonAndDeadlineWithoutSendingCodeAsync()
    {
        await using RecoveryTestHost host = await RecoveryTestHost.CreateAsync();
        await using AtariGoDbContext context = host.Database.CreateContext();
        DateTime now = host.Clock.GetUtcNow().UtcDateTime;
        var game = new Game
        {
            GameConfiguration = new GameConfiguration
            {
                CaptureGoal = 1, GameTimeSeconds = 600, TurnTimeSeconds = 30
            },
            GameType = "Public", State = "Waiting"
        };
        var report = new Report
        {
            ReporterId = 2, ReportedUserId = 1, Game = game,
            Category = "Inactivity", CreatedAt = now
        };
        context.Sanctions.Add(new Sanction
        {
            UserId = 1, TriggeringReport = report, SanctionType = "TemporarySuspension",
            Threshold = 3, StartedAt = now, EndsAt = now.AddDays(1)
        });
        await context.SaveChangesAsync();
        LoginResponse result = await LoginAsync(host);
        Assert.AreEqual(LoginResult.AccountRestricted, result.Result);
        Assert.AreEqual("Inactivity", result.RestrictionReason);
        Assert.AreEqual(0, host.Email.Codes.Count);
        Assert.IsGreaterThan(0, result.RetryAtUnixSeconds);
    }

    [TestMethod]
    public async Task VerifyLoginCode_AccountInactiveAfterIssuance_DoesNotCreateSessionAsync()
    {
        await using RecoveryTestHost host = await RecoveryTestHost.CreateAsync();
        LoginResponse pending = await LoginAsync(host);
        await using AtariGoDbContext context = host.Database.CreateContext();
        await context.Users.Where(user => user.UserId == 1)
            .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.State, "Inactive"));
        LoginResponse result = await VerifyAsync(host, pending.ChallengeToken,
            host.Email.Codes.Last());
        Assert.AreEqual(LoginResult.AccountRestricted, result.Result);
        Assert.AreEqual(string.Empty, result.SessionToken);
    }

    [TestMethod]
    public async Task VerifyLoginCode_PasswordChanged_InvalidatesPendingLoginAsync()
    {
        await using RecoveryTestHost host = await RecoveryTestHost.CreateAsync();
        LoginResponse pending = await LoginAsync(host);
        using IServiceScope scope = host.Services.CreateScope();
        AtariGoDbContext context = scope.ServiceProvider.GetRequiredService<AtariGoDbContext>();
        var credentials = new CredentialsService(context,
            new Microsoft.AspNetCore.Identity.PasswordHasher<User>(), host.Clock);
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
        Assert.AreEqual(ChangePasswordResult.Changed, changed);
        Assert.AreEqual(LoginResult.InvalidChallenge, result.Result);
    }

    [TestMethod]
    public async Task VerifyLoginCode_PasswordRecovered_InvalidatesPendingLoginAsync()
    {
        await using RecoveryTestHost host = await RecoveryTestHost.CreateAsync();
        LoginResponse pending = await LoginAsync(host);
        string loginCode = host.Email.Codes.Last();
        await host.RequestAsync();
        PasswordRecoveryResponse recovered = await host.ResetAsync(host.CreateReset());
        LoginResponse result = await VerifyAsync(host, pending.ChallengeToken, loginCode);
        Assert.AreEqual(RecoveryStatus.Reset, recovered.Status);
        Assert.AreEqual(LoginResult.InvalidChallenge, result.Result);
    }

    [TestMethod]
    public async Task CancelLogin_ValidAttempt_InvalidatesCodeAndDoesNotCountFailureAsync()
    {
        await using RecoveryTestHost host = await RecoveryTestHost.CreateAsync();
        LoginResponse pending = await LoginAsync(host);
        var client = new Contracts.Authentication.AuthenticationClient(host.Channel);
        await client.CancelLoginAsync(new CancelLoginRequest
        {
            ChallengeToken = pending.ChallengeToken
        });
        LoginResponse result = await VerifyAsync(host, pending.ChallengeToken,
            host.Email.Codes.Last());
        Assert.AreEqual(LoginResult.InvalidChallenge, result.Result);
        await using AtariGoDbContext context = host.Database.CreateContext();
        Assert.AreEqual(0, await context.Credentials.Where(item => item.UserId == 1)
            .Select(item => item.FailedAttempts).SingleAsync());
    }

    [TestMethod]
    public async Task ResendLoginCode_TooSoonOrAfterIssuanceWindow_DoesNotSendEmailAsync()
    {
        await using RecoveryTestHost host = await RecoveryTestHost.CreateAsync();
        LoginResponse pending = await LoginAsync(host);
        LoginResponse early = await ResendAsync(host, pending.ChallengeToken);
        LoginResponse freshLogin = await LoginAsync(host);
        host.Clock.Advance(TimeSpan.FromMinutes(30));
        LoginResponse late = await ResendAsync(host, pending.ChallengeToken);
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
            Identifier = identifier,
            Password = password,
            CultureName = "es-MX"
        }, deadline: DateTime.UtcNow.AddSeconds(30)).ResponseAsync;
    }

    private static Task<LoginResponse> VerifyAsync(RecoveryTestHost host, string token, string code)
    {
        var client = new Contracts.Authentication.AuthenticationClient(host.Channel);
        return client.VerifyLoginCodeAsync(new VerifyLoginCodeRequest
        {
            ChallengeToken = token,
            Code = code
        }, deadline: DateTime.UtcNow.AddSeconds(30)).ResponseAsync;
    }

    private static Task<LoginResponse> ResendAsync(RecoveryTestHost host, string token)
    {
        var client = new Contracts.Authentication.AuthenticationClient(host.Channel);
        return client.ResendLoginCodeAsync(new ResendLoginCodeRequest
        {
            ChallengeToken = token,
            CultureName = "es-MX"
        }, deadline: DateTime.UtcNow.AddSeconds(30)).ResponseAsync;
    }
}
