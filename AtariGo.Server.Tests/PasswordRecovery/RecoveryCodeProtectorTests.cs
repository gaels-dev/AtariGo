using Microsoft.Extensions.Options;

using AtariGo.Server.Application.PasswordRecovery;

namespace AtariGo.Server.Tests.PasswordRecovery;

[TestClass]
[TestCategory("Unit")]
public sealed class RecoveryCodeProtectorTests
{
    [TestMethod]
    public void Matches_PreviouslyIssuedCode_UsesExistingRecoveryKeyAndHashFormat()
    {
        var options = new PasswordRecoveryOptions { HmacKey = new string('K', 64) };
        var protector = new RecoveryCodeProtector(Options.Create(options));
        const string previousHash =
            "9C21EEE35502E4CBF0C2DFBA1870C282A043C1B23AE366B2A129D415E59C1782";
        bool matches = protector.Matches(1, "012345", previousHash);
        string currentHash = protector.Protect(1, "012345");
        Assert.IsTrue(matches);
        Assert.AreEqual(previousHash, currentHash);
        Assert.AreEqual(TimeSpan.FromMinutes(10), RecoveryCodeProtector.Lifetime);
        Assert.AreEqual(5, RecoveryCodeProtector.MaximumAttempts);
    }
}
