using AtariGo.Server.Application.Email;

namespace AtariGo.Server.Tests.Email;

[TestClass]
[TestCategory("Unit")]
public sealed class AuthenticationEmailContentTests
{
    [TestMethod]
    [DataRow("es-MX", "Tu código", "Verificación")]
    [DataRow("de-DE", "Dein Anmeldecode", "Anmeldebestätigung")]
    [DataRow("en-US", "Your login code", "Login verification")]
    [DataRow("unknown", "Your login code", "Login verification")]
    public void CreateMessage_LoginCulture_FormatsLocalizedEmail(string culture, string body,
        string subject)
    {
        EmailMessage message = AuthenticationEmailContent.Login.CreateMessage("owner@example.test",
            "012345", culture);
        AssertMessage(message, body, subject);
    }

    [TestMethod]
    [DataRow("es-MX", "Tu código", "Recuperación")]
    [DataRow("de-DE", "Dein Wiederherstellungscode", "Passwortwiederherstellung")]
    [DataRow("en-US", "Your recovery code", "Password recovery")]
    [DataRow("unknown", "Your recovery code", "Password recovery")]
    public void CreateMessage_RecoveryCulture_FormatsLocalizedEmail(string culture, string body,
        string subject)
    {
        EmailMessage message = AuthenticationEmailContent.Recovery.CreateMessage(
            "owner@example.test", "012345", culture);
        AssertMessage(message, body, subject);
    }

    [TestMethod]
    public void CreateMessage_ExcessiveCulture_UsesEnglishFallback()
    {
        EmailMessage message = AuthenticationEmailContent.Recovery.CreateMessage(
            "owner@example.test", "012345", new string('x', 100_000));
        AssertMessage(message, "Your recovery code", "Password recovery");
    }

    private static void AssertMessage(EmailMessage message, string body, string subject)
    {
        Assert.AreEqual("owner@example.test", message.Recipient);
        StringAssert.Contains(message.Subject, subject);
        StringAssert.Contains(message.Body, body);
        StringAssert.Contains(message.Body, "012345");
        Assert.IsFalse(message.Body.Contains("{0}", StringComparison.Ordinal));
    }
}
