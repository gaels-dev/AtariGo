using AtariGo.Server.Application.Email;

namespace AtariGo.Server.Tests.PasswordRecovery;

[TestClass]
[TestCategory("Unit")]
public sealed class RecoveryEmailContentTests
{
    [TestMethod]
    public void CreateMessage_SupportedAndUnknownCultures_FormatsLocalizedEmailWithEnglishFallback()
    {
        // Arrange
        var cases = new[]
        {
            ("es-MX", "Tu código", "Recuperación"),
            ("de-DE", "Dein Wiederherstellungscode", "Passwortwiederherstellung"),
            ("en-US", "Your recovery code", "Password recovery"),
            ("unknown", "Your recovery code", "Password recovery"),
            (new string('x', 100_000), "Your recovery code", "Password recovery")
        };
        foreach ((string culture, string expectedBody, string expectedSubject) in cases)
        {
            // Act
            EmailMessage message = RecoveryEmailContent.CreateMessage("owner@example.test",
                "12345678", culture);
            // Assert
            Assert.AreEqual("owner@example.test", message.Recipient);
            StringAssert.Contains(message.Body, expectedBody);
            StringAssert.Contains(message.Body, "12345678");
            StringAssert.Contains(message.Subject, expectedSubject);
            Assert.IsFalse(message.Body.Contains("{0}", StringComparison.Ordinal));
        }
    }
}
