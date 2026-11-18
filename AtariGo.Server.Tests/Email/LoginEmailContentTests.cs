using AtariGo.Server.Application.Email;

namespace AtariGo.Server.Tests.Email;

[TestClass]
[TestCategory("Unit")]
public sealed class LoginEmailContentTests
{
    [TestMethod]
    [DataRow("es-MX", "Tu código", "Verificación")]
    [DataRow("de-DE", "Dein Anmeldecode", "Anmeldebestätigung")]
    [DataRow("en-US", "Your login code", "Login verification")]
    [DataRow("unknown", "Your login code", "Login verification")]
    public void CreateMessage_Culture_ProducesLocalizedEmail(string culture, string body,
        string subject)
    {
        // Arrange
        const string code = "01234567";
        // Act
        EmailMessage message = LoginEmailContent.CreateMessage("player@example.test", code,
            culture);
        // Assert
        StringAssert.Contains(message.Subject, subject);
        StringAssert.Contains(message.Body, body);
        StringAssert.Contains(message.Body, code);
        Assert.AreEqual("player@example.test", message.Recipient);
        Assert.IsFalse(message.Body.Contains("{0}", StringComparison.Ordinal));
    }
}
