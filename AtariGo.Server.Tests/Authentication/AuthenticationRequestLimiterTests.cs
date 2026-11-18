using Microsoft.Extensions.Options;

using AtariGo.Server.Application.Authentication;
using AtariGo.Server.Application.PasswordRecovery;

namespace AtariGo.Server.Tests.Authentication;

[TestClass]
[TestCategory("Unit")]
public sealed class AuthenticationRequestLimiterTests
{
    [TestMethod]
    public void TryAcquire_ExhaustedLoginQuota_PreservesRecoveryAndOtherAddresses()
    {
        using var limiter = new AuthenticationRequestLimiter(
            Options.Create(new LoginVerificationOptions { IpRequestsPerHour = 1 }),
            Options.Create(new PasswordRecoveryOptions { IpRequestsPerHour = 2 }));
        bool firstLogin = limiter.TryAcquireLogin("first");
        bool secondLogin = limiter.TryAcquireLogin("first");
        bool firstRecovery = limiter.TryAcquireRecovery("first");
        bool secondRecovery = limiter.TryAcquireRecovery("first");
        bool thirdRecovery = limiter.TryAcquireRecovery("first");
        bool otherLogin = limiter.TryAcquireLogin("second");
        Assert.IsTrue(firstLogin);
        Assert.IsFalse(secondLogin);
        Assert.IsTrue(firstRecovery);
        Assert.IsTrue(secondRecovery);
        Assert.IsFalse(thirdRecovery);
        Assert.IsTrue(otherLogin);
    }
}
