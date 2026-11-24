using System.Text.Json;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using AtariGo.Contracts;
using AtariGo.Diagnostics;
using AtariGo.Server.Application.Authentication;
using AtariGo.Server.Application.PasswordRecovery;
using AtariGo.Server.Services;
using AtariGo.Server.Tests.Games;

namespace AtariGo.Server.Tests.Diagnostics;

[TestClass]
[TestCategory("Integration")]
[OSCondition(OperatingSystems.Windows)]
public sealed class IdentityLoggingTests
{
    private readonly TestContext _testContext;

    public IdentityLoggingTests(TestContext testContext)
    {
        ArgumentNullException.ThrowIfNull(testContext);
        _testContext = testContext;
    }

    [TestMethod]
    public async Task Login_ExhaustedLimiter_RecordsRejectionWithoutCallingAuthenticationAsync()
    {
        await using var host = new ServerLogTestHost(fileLevel: "Information");
        var authentication = new StubIdentityAuthentication();
        using var limiter = new AuthenticationRequestLimiter(
            Options.Create(new LoginVerificationOptions { IpRequestsPerHour = 1 }),
            Options.Create(new PasswordRecoveryOptions()));
        var service = new AuthenticationGrpcService(authentication, limiter,
            host.Application.Services.GetRequiredService<ILogger<AuthenticationGrpcService>>());
        var context = new GameSessionTestCallContext("private");
        context.UserState["__HttpContext"] = new DefaultHttpContext();
        LoginResponse success = await service.Login(new LoginRequest
        {
            Identifier = "private",
            Password = "private"
        }, context);
        LoginResponse rejected = await service.Login(new LoginRequest(), context);
        await service.CancelLogin(new CancelLoginRequest { ChallengeToken = "private" }, context);
        List<JsonElement> entries = await host.ReadEventsAsync(_testContext.CancellationToken);

        Assert.AreEqual(LoginResult.Success, success.Result);
        Assert.AreEqual(LoginResult.RateLimited, rejected.Result);
        Assert.AreEqual(1, authentication.Calls);
        Assert.HasCount(3, entries);
        Assert.AreEqual("Succeeded", entries[0].GetProperty("Outcome").GetString());
        Assert.AreEqual(7, entries[0].GetProperty("UserId").GetInt32());
        Assert.AreEqual("Rejected", entries[1].GetProperty("Outcome").GetString());
        Assert.AreEqual("CancelLogin", entries[2].GetProperty("Operation").GetString());
        Assert.AreEqual("Rejected", entries[2].GetProperty("Outcome").GetString());
        Assert.IsTrue(entries.All(entry =>
            !entry.GetRawText().Contains("private", StringComparison.Ordinal)));
    }
}
