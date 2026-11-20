using System.Globalization;

using AtariGo.Client.Models;
using AtariGo.Client.Services;

namespace AtariGo.Client.Tests.Services;

[TestClass]
[TestCategory("Unit")]
public sealed class AuthenticationMessagesTests
{
    [TestMethod]
    [DataRow("es")]
    [DataRow("en")]
    [DataRow("de")]
    public void GetMessage_ChallengeRequired_OmitsRetryDateAndFormatsLifetime(string culture)
    {
        CultureInfo previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            string message = AuthenticationMessages.GetMessage(new LoginAttemptResult
            {
                Status = LoginAttemptStatus.ChallengeRequired,
                RetryAtUnixSeconds = 1800000000
            });

            Assert.IsFalse(message.Contains('\n'));
            Assert.IsFalse(message.Contains('{'));
            Assert.AreEqual(2, message.Count(character => character == '.'));
            StringAssert.Contains(message, "10");
            if (culture == "es")
            {
                StringAssert.EndsWith(message, "Dura 10 minutos.");
            }
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [TestMethod]
    public void GetMessage_SpanishUnconfirmed_EndsAtRequestedSentence()
    {
        CultureInfo previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("es");
        try
        {
            string message = AuthenticationMessages.GetMessage(new LoginAttemptResult
            {
                Status = LoginAttemptStatus.Unconfirmed
            });

            StringAssert.EndsWith(message, "vuelve e inicia de nuevo.");
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }
}
