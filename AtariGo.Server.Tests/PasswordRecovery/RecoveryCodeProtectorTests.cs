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
        // Arrange
        var options = new PasswordRecoveryOptions { HmacKey = new string('K', 64) };
        var protector = new RecoveryCodeProtector(Options.Create(options));
        const string previousHash =
            "07DD21B1608634684E89426287BCC831AF61CDE9A01EB47F1C15B7E960124612";
        // Act
        bool matches = protector.Matches(1, "01234567", previousHash);
        string currentHash = protector.Protect(1, "01234567");
        // Assert
        Assert.IsTrue(matches);
        Assert.AreEqual(previousHash, currentHash);
        Assert.AreEqual(TimeSpan.FromMinutes(10), RecoveryCodeProtector.Lifetime);
        Assert.AreEqual(5, RecoveryCodeProtector.MaximumAttempts);
    }
}
