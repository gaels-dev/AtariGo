namespace AtariGo.Diagnostics.Tests;

[TestClass]
[TestCategory("Unit")]
public sealed class GameDiagnosticsTests
{
    [TestMethod]
    [DataRow("CaptureGoal", GameDiagnosticReason.CaptureGoal)]
    [DataRow("private-input", GameDiagnosticReason.Unspecified)]
    public void CreateReason_UntrustedText_ProducesOnlyCataloguedValues(string text,
        GameDiagnosticReason expected)
    {
        // Arrange
        var details = new DiagnosticEventDetails { Status = GameDiagnostics.CreateReason(text) };
        // Act
        DiagnosticEvent entry = GameDiagnostics.Create(GameOperation.Finished, details);
        // Assert
        Assert.AreEqual("AtariGo.Server.Game", entry.Definition.Source);
        Assert.AreEqual(expected.ToString(), entry.Details.Status?.Value);
        Assert.AreEqual((int)GameOperation.Finished, entry.Definition.Id);
    }
}
