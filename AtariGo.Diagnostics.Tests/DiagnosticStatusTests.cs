namespace AtariGo.Diagnostics.Tests;

[TestClass]
[TestCategory("Unit")]
public sealed class DiagnosticStatusTests
{
    [TestMethod]
    public void Constructor_DefinedEnum_PreservesTechnicalStatus()
    {
        // Arrange
        DiagnosticOutcome value = DiagnosticOutcome.Recovered;
        // Act
        var status = new DiagnosticStatus(value);
        // Assert
        Assert.AreEqual(nameof(DiagnosticOutcome), status.TypeName);
        Assert.AreEqual(nameof(DiagnosticOutcome.Recovered), status.Value);
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(100)]
    public void Constructor_UndefinedEnum_RejectsInput(int value)
    {
        // Arrange
        DiagnosticOutcome invalid = (DiagnosticOutcome)value;
        // Act
        ArgumentOutOfRangeException error = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new DiagnosticStatus(invalid));
        // Assert
        Assert.AreEqual("value", error.ParamName);
    }
}
