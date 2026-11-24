namespace AtariGo.Diagnostics.Tests;

[TestClass]
[TestCategory("Unit")]
public sealed class PlayerDiagnosticsTests
{
    [TestMethod]
    [DataRow(0L)]
    [DataRow(-1L)]
    public void Create_InvalidReportIdentifier_RejectsAmbiguousContext(long reportId)
    {
        // Arrange
        var details = new DiagnosticEventDetails
        {
            Context = new DiagnosticContext { ReportId = reportId }
        };
        // Act
        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PlayerDiagnostics.Create(PlayerOperation.ReportCommitted, details));
    }

    [TestMethod]
    [DataRow(PlayerOperation.Unspecified)]
    [DataRow((PlayerOperation)int.MaxValue)]
    public void Create_UnknownOperation_RejectsUnstableIdentity(PlayerOperation operation)
    {
        // Arrange
        var details = new DiagnosticEventDetails();
        // Act
        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PlayerDiagnostics.Create(operation, details));
    }
}
