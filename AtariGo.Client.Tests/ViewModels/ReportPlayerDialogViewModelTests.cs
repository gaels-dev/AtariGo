using System.Globalization;

using AtariGo.Client.Models;
using AtariGo.Client.Tests.Services;
using AtariGo.Client.ViewModels.Dialogs;

namespace AtariGo.Client.Tests.ViewModels;

[TestClass]
[TestCategory("Unit")]
public sealed class ReportPlayerDialogViewModelTests
{
    [TestMethod]
    [DataRow(199, true)]
    [DataRow(200, true)]
    [DataRow(201, false)]
    [DataRow(2000, false)]
    public async Task SubmitCommand_DescriptionBoundary_SendsOnlyAllowedLengthsAsync(
        int length, bool accepted)
    {
        StubReportClient client = new StubReportClient();
        using ReportPlayerDialogViewModel model = new ReportPlayerDialogViewModel("Target", 1,
            client);
        model.Details = new string('ñ', length);

        await CommandExecution.ExecuteAsync(model.SubmitCommand, null);

        Assert.AreEqual(200, model.MaximumDetailsLength);
        Assert.AreEqual(!accepted, model.CanSubmit);
        if (accepted)
        {
            Assert.AreEqual(1, client.Calls);
            Assert.AreEqual(model.Details, client.ReceivedDraft!.Description);
        }
        else
        {
            Assert.AreEqual(0, client.Calls);
            Assert.IsTrue(model.StatusMessage.Contains("200", StringComparison.Ordinal));
        }
        Assert.AreEqual(length, model.Details.Length);
    }

    [TestMethod]
    public async Task SubmitCommand_ServerRejectsLength_ShowsSpecificLimitAndPreservesTextAsync()
    {
        StubReportClient client = new StubReportClient
        {
            ResponseStatus = PlayerReportStatus.DescriptionTooLong
        };
        using ReportPlayerDialogViewModel model = new ReportPlayerDialogViewModel("Target", 1,
            client);
        model.Details = "Report details";

        await CommandExecution.ExecuteAsync(model.SubmitCommand, null);

        Assert.IsTrue(model.StatusMessage.Contains("200", StringComparison.Ordinal));
        Assert.AreEqual("Report details", model.Details);
        Assert.IsTrue(model.CanSubmit);
    }

    [TestMethod]
    [DataRow("en", "characters")]
    [DataRow("es", "caracteres")]
    [DataRow("de", "Zeichen")]
    public void DetailsLengthLabel_SupportedCulture_UpdatesLocalizedCount(
        string culture, string expectedWord)
    {
        CultureInfo previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            using ReportPlayerDialogViewModel model = new ReportPlayerDialogViewModel(
                "Target", 1, new StubReportClient());
            model.Details = "á\r\n😀";

            Assert.IsTrue(model.DetailsLengthLabel.Contains("5/200", StringComparison.Ordinal));
            Assert.IsTrue(model.DetailsLengthLabel.Contains(expectedWord,
                StringComparison.Ordinal));
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }
}
