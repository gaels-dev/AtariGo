using System.Globalization;
using System.Windows;

using AtariGo.Client.Models;
using AtariGo.Client.Tests.Services;
using AtariGo.Client.ViewModels.Dialogs;
using AtariGo.Client.Views.Dialogs;

namespace AtariGo.Client.Tests.Views;

[TestClass]
[TestCategory("Unit")]
public sealed class LoginVerificationDialogViewTests
{
    [STATestMethod]
    [DataRow("en")]
    [DataRow("es")]
    [DataRow("de")]
    public void View_SupportedCulture_InitializesWithLocalizedText(string culture)
    {
        CultureInfo previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            var pending = new LoginAttemptResult
            {
                Status = LoginAttemptStatus.ChallengeRequired,
                ChallengeToken = "pending"
            };
            var client = new StubAuthenticationClient(_ => Task.FromResult(pending));
            using var model = new LoginVerificationDialogViewModel(client, pending, _ => { });
            var view = new LoginVerificationDialogView { DataContext = model };
            view.Measure(new Size(430, 600));
            view.Arrange(new Rect(0, 0, 430, 600));
            view.UpdateLayout();
            Assert.IsTrue(view.IsInitialized);
            Assert.IsFalse(model.Title.StartsWith("LoginVerification_", StringComparison.Ordinal));
            Assert.IsFalse(model.Message.StartsWith("LoginVerification_",
                StringComparison.Ordinal));
            Assert.IsTrue(view.DesiredSize.Height <= 600);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }
}
