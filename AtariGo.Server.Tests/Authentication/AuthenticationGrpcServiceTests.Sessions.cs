using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Grpc.Core;

using AtariGo.Contracts;
using AtariGo.Server.Application.Sessions;
using AtariGo.Server.Data;
using AtariGo.Server.Tests.PasswordRecovery;

namespace AtariGo.Server.Tests.Authentication;

public sealed partial class AuthenticationGrpcServiceTests
{
    [TestMethod]
    public async Task Login_ConnectedAccount_RejectsOnlyThatAccountWithoutSendingCodeAsync()
    {
        await using RecoveryTestHost host = await RecoveryTestHost.CreateAsync();
        using AsyncDuplexStreamingCall<SessionClientEvent, SessionServerEvent> connection =
            await ConnectAsync(host, "Owner");

        LoginResponse denied = await LoginAsync(host);
        Assert.AreEqual(LoginResult.AlreadySignedIn, denied.Result);
        Assert.AreEqual(string.Empty, denied.SessionToken);
        Assert.AreEqual(0, host.Email.Codes.Count);
        host.Email.ExpectedRecipient = "Other@example.test";
        LoginResponse other = await LoginAsync(host, "Other");
        LoginResponse confirmed = await VerifyAsync(host, other.ChallengeToken,
            host.Email.Codes.Last());
        using AsyncDuplexStreamingCall<SessionClientEvent, SessionServerEvent> second =
            await ConnectAsync(host, confirmed.SessionToken);
        Assert.AreEqual(LoginResult.Success, confirmed.Result);
        Assert.IsTrue(host.Services.GetRequiredService<ISessionConnectionRegistry>()
            .IsUserConnected(1));
    }

    [TestMethod]
    public async Task VerifyLoginCode_AccountConnectsAfterChallenge_RejectsUntilDisconnectAsync()
    {
        await using RecoveryTestHost host = await RecoveryTestHost.CreateAsync();
        LoginResponse pending = await LoginAsync(host);
        string code = host.Email.Codes.Last();
        using AsyncDuplexStreamingCall<SessionClientEvent, SessionServerEvent> connection =
            await ConnectAsync(host, "Owner");

        LoginResponse denied = await VerifyAsync(host, pending.ChallengeToken, code);
        Assert.AreEqual(LoginResult.AlreadySignedIn, denied.Result);
        await DisconnectAsync(connection);
        LoginResponse confirmed = await VerifyAsync(host, pending.ChallengeToken, code);

        Assert.AreEqual(LoginResult.Success, confirmed.Result);
        Assert.AreEqual(1, host.Email.Codes.Count);
        using AsyncDuplexStreamingCall<SessionClientEvent, SessionServerEvent> current =
            await ConnectAsync(host, confirmed.SessionToken);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Login_PreviousClientLeaves_AllowsImmediateAccessAndRevokesOldTokenAsync(
        bool logsOut)
    {
        await using RecoveryTestHost host = await RecoveryTestHost.CreateAsync();
        using AsyncDuplexStreamingCall<SessionClientEvent, SessionServerEvent> connection =
            await ConnectAsync(host, "Owner");
        if (logsOut)
        {
            var client = new Session.SessionClient(host.Channel);
            await client.LogoutAsync(new LogoutRequest(), CreateHeaders("Owner"),
                DateTime.UtcNow.AddSeconds(30));
        }
        await DisconnectAsync(connection);

        LoginResponse pending = await LoginAsync(host);
        LoginResponse confirmed = await VerifyAsync(host, pending.ChallengeToken,
            host.Email.Codes.Last());

        Assert.AreEqual(LoginResult.Success, confirmed.Result);
        using IServiceScope scope = host.Services.CreateScope();
        ISessionService sessions = scope.ServiceProvider.GetRequiredService<ISessionService>();
        Assert.IsNull(await sessions.ValidateSessionAsync("Owner", CancellationToken.None));
        Assert.IsNotNull(await sessions.ValidateSessionAsync(confirmed.SessionToken,
            CancellationToken.None));
        RpcException denied = await Assert.ThrowsAsync<RpcException>(() =>
            ConnectAsync(host, "Owner"));
        Assert.AreEqual(StatusCode.Unauthenticated, denied.StatusCode);
        using AsyncDuplexStreamingCall<SessionClientEvent, SessionServerEvent> current =
            await ConnectAsync(host, confirmed.SessionToken);
    }

    [TestMethod]
    public async Task Connect_TransientDisconnect_ReusesSessionUntilANewLoginAsync()
    {
        await using RecoveryTestHost host = await RecoveryTestHost.CreateAsync();
        using AsyncDuplexStreamingCall<SessionClientEvent, SessionServerEvent> connection =
            await ConnectAsync(host, "Owner");

        await DisconnectAsync(connection);
        using AsyncDuplexStreamingCall<SessionClientEvent, SessionServerEvent> reconnected =
            await ConnectAsync(host, "Owner");

        LoginResponse denied = await LoginAsync(host);
        Assert.AreEqual(LoginResult.AlreadySignedIn, denied.Result);
        Assert.AreEqual(0, host.Email.Codes.Count);
    }

    [TestMethod]
    public async Task VerifyLoginCode_ConcurrentAttempts_LeavesOnlyOneAuthorizedSessionAsync()
    {
        await using RecoveryTestHost host = await RecoveryTestHost.CreateAsync();
        LoginResponse first = await LoginAsync(host);
        string firstCode = host.Email.Codes.Last();
        host.Clock.Advance(TimeSpan.FromSeconds(60));
        LoginResponse second = await LoginAsync(host);
        string secondCode = host.Email.Codes.Last();

        LoginResponse[] results = await Task.WhenAll(
            VerifyAsync(host, first.ChallengeToken, firstCode),
            VerifyAsync(host, second.ChallengeToken, secondCode));

        Assert.IsTrue(results.All(result => result.Result == LoginResult.Success));
        await using AtariGoDbContext context = host.Database.CreateContext();
        Assert.AreEqual(1, await context.Sessions.CountAsync(session => session.UserId == 1
            && session.State == "Active"));
        using IServiceScope scope = host.Services.CreateScope();
        ISessionService sessions = scope.ServiceProvider.GetRequiredService<ISessionService>();
        int validTokens = 0;
        foreach (LoginResponse result in results)
        {
            if (await sessions.ValidateSessionAsync(result.SessionToken,
                CancellationToken.None) is not null)
            {
                validTokens++;
            }
        }
        Assert.AreEqual(1, validTokens);
    }

    [TestMethod]
    public async Task VerifyLoginCode_OldSessionReconnectsConcurrently_AuthorizesOnlyOneAsync()
    {
        await using RecoveryTestHost host = await RecoveryTestHost.CreateAsync();
        LoginResponse pending = await LoginAsync(host);

        Task<AsyncDuplexStreamingCall<SessionClientEvent, SessionServerEvent>> connecting =
            ConnectAsync(host, "Owner");
        Task<LoginResponse> verifying = VerifyAsync(host, pending.ChallengeToken,
            host.Email.Codes.Last());
        AsyncDuplexStreamingCall<SessionClientEvent, SessionServerEvent>? connection = null;
        try
        {
            connection = await connecting;
        }
        catch (RpcException exception)
        {
            Assert.AreEqual(StatusCode.Unauthenticated, exception.StatusCode);
        }
        using (connection)
        {
            LoginResponse result = await verifying;
            LoginResult expected = connection is null
                ? LoginResult.Success : LoginResult.AlreadySignedIn;
            Assert.AreEqual(expected, result.Result);
        }
    }

    [TestMethod]
    public async Task VerifyLoginCode_WriteFails_RollsBackSessionAndPreservesCodeAsync()
    {
        await using RecoveryTestHost host = await RecoveryTestHost.CreateAsync();
        LoginResponse pending = await LoginAsync(host);
        string code = host.Email.Codes.Last();
        await using AtariGoDbContext context = host.Database.CreateContext();
        await context.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER dbo.FailSession ON dbo.Sessions AFTER INSERT AS
            THROW 51000, 'Simulated session write failure.', 1;
            """);

        await Assert.ThrowsAsync<RpcException>(() => VerifyAsync(host,
            pending.ChallengeToken, code));

        using IServiceScope scope = host.Services.CreateScope();
        ISessionService sessions = scope.ServiceProvider.GetRequiredService<ISessionService>();
        Assert.IsNotNull(await sessions.ValidateSessionAsync("Owner", CancellationToken.None));
        await context.Database.ExecuteSqlRawAsync("DROP TRIGGER dbo.FailSession;");
        LoginResponse confirmed = await VerifyAsync(host, pending.ChallengeToken, code);
        Assert.AreEqual(LoginResult.Success, confirmed.Result);
    }

    private static async Task<AsyncDuplexStreamingCall<SessionClientEvent, SessionServerEvent>>
        ConnectAsync(RecoveryTestHost host, string token)
    {
        var client = new Session.SessionClient(host.Channel);
        AsyncDuplexStreamingCall<SessionClientEvent, SessionServerEvent> connection =
            client.Connect(CreateHeaders(token), deadline: DateTime.UtcNow.AddSeconds(30));
        try
        {
            await connection.RequestStream.WriteAsync(new SessionClientEvent
            {
                ClientConnected = new ClientConnected()
            });
            Assert.IsTrue(await connection.ResponseStream.MoveNext(CancellationToken.None));
            Assert.IsNotNull(connection.ResponseStream.Current.FriendPresenceSnapshot);
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private static async Task DisconnectAsync(
        AsyncDuplexStreamingCall<SessionClientEvent, SessionServerEvent> connection)
    {
        await connection.RequestStream.CompleteAsync();
        Assert.IsFalse(await connection.ResponseStream.MoveNext(CancellationToken.None));
    }

    private static Metadata CreateHeaders(string token)
    {
        return new Metadata { { "authorization", "Bearer " + token } };
    }
}
