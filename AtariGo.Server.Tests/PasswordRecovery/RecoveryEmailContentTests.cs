using AtariGo.Server.Application.Email;

namespace AtariGo.Server.Tests.PasswordRecovery;

[TestClass]
[TestCategory("Unit")]
public sealed class RecoveryEmailContentTests
{
    [TestMethod]
    public void GetBody_SupportedAndUnknownCultures_FormatsLocalizedCodeWithEnglishFallback()
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
            var message = new RecoveryEmailMessage { CultureName = culture, Code = "12345678" };
            // Act
            string body = RecoveryEmailContent.GetBody(message);
            string subject = RecoveryEmailContent.GetSubject(culture);
            // Assert
            StringAssert.Contains(body, expectedBody);
            StringAssert.Contains(body, message.Code);
            StringAssert.Contains(subject, expectedSubject);
            Assert.IsFalse(body.Contains("{0}", StringComparison.Ordinal));
        }
    }
}
