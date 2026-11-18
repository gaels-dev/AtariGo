using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Grpc.Core;

using AtariGo.Contracts;
using AtariGo.Server.Application.Authentication;
using AtariGo.Server.Application.Games;
using AtariGo.Server.Application.PasswordRecovery;
using AtariGo.Server.Application.Sessions;
using AtariGo.Server.Data;
using AtariGo.Server.Data.Entities;

namespace AtariGo.Server.Tests.PasswordRecovery;

[TestClass]
[TestCategory("Integration")]
public sealed class PasswordRecoveryTests
{
    [TestMethod]
    public async Task
        ResetPassword_ValidCode_ChangesCredentialRevokesConnectionsAndRejectsReuseAsync()
    {
        await using RecoveryTestHost host = await RecoveryTestHost.CreateAsync();
        await host.RequestAsync("Owner@example.test");
        var sessions = new Contracts.Session.SessionClient(host.Channel);
        using AsyncDuplexStreamingCall<SessionClientEvent, SessionServerEvent> stream =
            sessions.Connect(new Metadata { { "authorization", "Bearer Owner" } },
                deadline: DateTime.UtcNow.AddSeconds(60));
        await stream.RequestStream.WriteAsync(new SessionClientEvent
        {
            ClientConnected = new ClientConnected()
        });
        Assert.IsTrue(await stream.ResponseStream.MoveNext(CancellationToken.None));
        IGameSessionRegistry games = host.Services.GetRequiredService<IGameSessionRegistry>();
        using GameSessionConnection game = games.Connect(CreateGame());
        PasswordRecoveryResponse result = await host.ResetAsync(host.CreateReset());
        PasswordRecoveryResponse reused = await host.ResetAsync(host.CreateReset());
        bool ended = !await stream.ResponseStream.MoveNext(CancellationToken.None);
        Assert.AreEqual(RecoveryStatus.Reset, result.Status);
        Assert.AreEqual(RecoveryStatus.InvalidCode, reused.Status);
        Assert.IsTrue(ended);
        Assert.IsTrue(game.RevocationToken.IsCancellationRequested);
        Assert.IsFalse(games.IsCurrent(game));
        Assert.IsNotNull(host.Email.LastMessage);
        Assert.AreEqual(string.Empty, host.Email.LastMessage.Body);
        await AssertSavedStateAsync(host);
    }

    [TestMethod]
    public async Task ResetPassword_PreviouslyIssuedCode_ResetsWithoutSendingAnotherEmailAsync()
    {
        await using RecoveryTestHost host = await RecoveryTestHost.CreateAsync();
        await using AtariGoDbContext context = host.Database.CreateContext();
        DateTime now = host.Clock.GetUtcNow().UtcDateTime;
        context.AuthenticationCodes.Add(new AuthenticationCode
        {
            UserId = 1,
            CodeType = "PasswordReset",
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(10),
            CodeHash = "07DD21B1608634684E89426287BCC831AF61CDE9A01EB47F1C15B7E960124612"
        });
        await context.SaveChangesAsync();
        PasswordRecoveryResponse result = await host.ResetAsync(host.CreateReset("01234567"));
        Assert.AreEqual(RecoveryStatus.Reset, result.Status);
        Assert.AreEqual(0, host.Email.Codes.Count);
        Assert.IsNotNull(await context.AuthenticationCodes.AsNoTracking()
            .Select(code => code.UsedAt).SingleAsync());
        await AssertSavedStateAsync(host);
    }

    [TestMethod]
    public async Task ResetPassword_InvalidCodesAndPasswords_PreservesCredentialAsync()
    {
        await using RecoveryTestHost host = await RecoveryTestHost.CreateAsync();
        await host.RequestAsync();
        ResetPasswordRequest request = host.CreateReset();
        await using AtariGoDbContext context = host.Database.CreateContext();
        string original = await context.Credentials.Where(row => row.UserId == 1)
            .Select(row => row.PasswordHash).SingleAsync();
        request.NewPassword = "lowercase";
        Assert.AreEqual(RecoveryStatus.InvalidPassword, (await host.ResetAsync(request)).Status);
        request.NewPassword = RecoveryTestHost.NewPassword;
        request.Confirmation = "Different9";
        Assert.AreEqual(RecoveryStatus.ConfirmationMismatch,
            (await host.ResetAsync(request)).Status);
        request.Confirmation = request.NewPassword;
        request.Identifier = "Other";
        Assert.AreEqual(RecoveryStatus.InvalidCode, (await host.ResetAsync(request)).Status);
        request.Identifier = "Owner";
        await context.AuthenticationCodes.ExecuteUpdateAsync(setters => setters
            .SetProperty(row => row.CodeType, "TwoFactor"));
        Assert.AreEqual(RecoveryStatus.InvalidCode, (await host.ResetAsync(request)).Status);
        await context.AuthenticationCodes.ExecuteUpdateAsync(setters => setters
            .SetProperty(row => row.CodeType, RecoveryCodeProtector.Purpose));
        request.Code = "incorrect";
        for (int attempt = 0; attempt < RecoveryCodeProtector.MaximumAttempts; attempt++)
        {
            Assert.AreEqual(RecoveryStatus.InvalidCode, (await host.ResetAsync(request)).Status);
        }
        Assert.AreEqual(RecoveryStatus.InvalidCode,
            (await host.ResetAsync(host.CreateReset())).Status);
        Assert.AreEqual(original, await context.Credentials.Where(row => row.UserId == 1)
            .Select(row => row.PasswordHash).SingleAsync());
        AuthenticationCode saved = await context.AuthenticationCodes.SingleAsync();
        Assert.AreEqual(5, saved.AttemptCount);
        Assert.IsNotNull(saved.UsedAt);
        Assert.IsTrue(await context.Sessions.AllAsync(row => row.State == "Active"));
    }

    [TestMethod]
    public async Task RequestPasswordRecovery_ResendExpiryAndDeliveryFailure_ProtectsCodesAsync()
    {
        await using RecoveryTestHost host = await RecoveryTestHost.CreateAsync();
        await host.RequestAsync("Unknown");
        Assert.AreEqual(0, host.Email.Codes.Count);
        await host.RequestAsync();
        string first = host.Email.Codes.Single();
        await host.RequestAsync();
        Assert.AreEqual(1, host.Email.Codes.Count);
        host.Clock.Advance(TimeSpan.FromSeconds(60));
        await host.RequestAsync();
        Assert.AreEqual(RecoveryStatus.InvalidCode,
            (await host.ResetAsync(host.CreateReset(first))).Status);
        host.Clock.Advance(TimeSpan.FromMinutes(10));
        Assert.AreEqual(RecoveryStatus.InvalidCode,
            (await host.ResetAsync(host.CreateReset())).Status);
        host.Email.FailDelivery = true;
        Assert.AreEqual(RecoveryStatus.DeliveryFailed, (await host.RequestAsync()).Status);
        Assert.IsNotNull(host.Email.LastMessage);
        Assert.AreEqual(string.Empty, host.Email.LastMessage.Body);
        host.Services.GetRequiredService<IOptions<PasswordRecoveryOptions>>()
            .Value.AccountRequestsPerHour = 3;
        host.Clock.Advance(TimeSpan.FromSeconds(60));
        host.Email.FailDelivery = false;
        await host.RequestAsync();
        await using AtariGoDbContext context = host.Database.CreateContext();
        List<AuthenticationCode> codes = await context.AuthenticationCodes.ToListAsync();
        Assert.AreEqual(3, codes.Count);
        Assert.AreEqual(2, host.Email.Codes.Count);
        Assert.IsFalse(host.Services.GetRequiredService<RecoveryCodeProtector>()
            .Matches(2, first, codes[0].CodeHash));
        Assert.IsTrue(codes.All(row => row.ExpiresAt - row.CreatedAt == TimeSpan.FromMinutes(10)));
        Assert.IsNotNull(codes.Last().UsedAt);
        Assert.IsTrue(codes.All(row => row.CodeHash.Length == 64 && row.CodeHash != first));
        Assert.AreEqual(8, first.Length);
        Assert.IsTrue(first.All(char.IsAsciiDigit));
    }

    [TestMethod]
    public async Task ResetPassword_FailedWriteAndConcurrentRequests_PreservesAtomicityAsync()
    {
        await using RecoveryTestHost host = await RecoveryTestHost.CreateAsync();
        await host.RequestAsync();
        await using AtariGoDbContext context = host.Database.CreateContext();
        await context.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER dbo.FailPasswordReset ON dbo.Credentials AFTER UPDATE AS
            THROW 51000, 'Simulated credential write failure.', 1;
            """);
        PasswordRecoveryResponse failure = await host.ResetAsync(host.CreateReset());
        Assert.AreEqual(RecoveryStatus.NotSaved, failure.Status);
        Assert.IsNull((await context.AuthenticationCodes.AsNoTracking().SingleAsync()).UsedAt);
        Assert.IsNull((await context.Credentials.AsNoTracking().FirstAsync()).PasswordChangedAt);
        Assert.IsTrue(await context.Sessions.AllAsync(row => row.State == "Active"));
        await context.Database.ExecuteSqlRawAsync("DROP TRIGGER dbo.FailPasswordReset;");
        PasswordRecoveryResponse[] results = await Task.WhenAll(
            host.ResetAsync(host.CreateReset()), host.ResetAsync(host.CreateReset()));
        Assert.AreEqual(1, results.Count(result => result.Status == RecoveryStatus.Reset));
        Assert.AreEqual(1, results.Count(result => result.Status == RecoveryStatus.InvalidCode));
        await AssertSavedStateAsync(host);
    }

    private static GameSessionDefinition CreateGame()
    {
        return new GameSessionDefinition
        {
            GameId = 1,
            CaptureGoal = 1,
            GameTimeSeconds = 300,
            TurnTimeSeconds = 30,
            Player = new GameSessionParticipant("user:1", "Owner", "Black"),
            Opponent = new GameSessionParticipant("user:2", "Other", "White")
        };
    }

    private static async Task AssertSavedStateAsync(RecoveryTestHost host)
    {
        await using AtariGoDbContext context = host.Database.CreateContext();
        User user = await context.Users.Include(row => row.Credential)
            .SingleAsync(row => row.UserId == 1);
        Assert.IsNotNull(user.Credential!.PasswordChangedAt);
        var hasher = new PasswordHasher<User>();
        Assert.AreEqual(PasswordVerificationResult.Success,
            hasher.VerifyHashedPassword(user, user.Credential.PasswordHash,
                RecoveryTestHost.NewPassword));
        Assert.AreEqual("Revoked", await context.Sessions.Where(row => row.UserId == 1)
            .Select(row => row.State).SingleAsync());
        Assert.AreEqual("Active", await context.Sessions.Where(row => row.UserId == 2)
            .Select(row => row.State).SingleAsync());
        using IServiceScope scope = host.Services.CreateScope();
        Assert.IsNull(await scope.ServiceProvider.GetRequiredService<ISessionService>()
            .ValidateSessionAsync("Owner", CancellationToken.None));
        IAuthenticationService login = scope.ServiceProvider
            .GetRequiredService<IAuthenticationService>();
        Assert.AreEqual(LoginStatus.InvalidCredentials, (await login.LoginAsync(new LoginCommand
        {
            Identifier = "Owner",
            Password = RecoveryTestHost.OriginalPassword
        }, CancellationToken.None)).Status);
        Assert.AreEqual(LoginStatus.ChallengeRequired, (await login.LoginAsync(new LoginCommand
        {
            Identifier = "Owner",
            Password = RecoveryTestHost.NewPassword
        }, CancellationToken.None)).Status);
    }
}
