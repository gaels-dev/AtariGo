namespace AtariGo.Diagnostics.Tests;

[TestClass]
[TestCategory("Unit")]
public sealed class DiagnosticEventDefinitionTests
{
    [TestMethod]
    public void Constructor_TechnicalNames_PreservesDefinition()
    {
        // Arrange
        const string source = "AtariGo.Server.Games";
        const string operation = "GameStream_Connected2";
        // Act
        var definition = new DiagnosticEventDefinition(1001, source, operation);
        // Assert
        Assert.AreEqual(1001, definition.Id);
        Assert.AreEqual(source, definition.Source);
        Assert.AreEqual(operation, definition.Operation);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void Constructor_NonpositiveId_RejectsDefinition(int id)
    {
        // Arrange
        const string source = "AtariGo.Client";
        // Act
        ArgumentOutOfRangeException exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => _ = new DiagnosticEventDefinition(id, source, "Login"));
        // Assert
        Assert.AreEqual("id", exception.ParamName);
    }

    [TestMethod]
    [DataRow(null, "Login", "source")]
    [DataRow("AtariGo.Client", null, "operation")]
    public void Constructor_NullName_RejectsDefinition(string? source, string? operation,
        string parameterName)
    {
        // Arrange
        const int eventId = 1001;
        // Act
        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => _ = new DiagnosticEventDefinition(eventId, source!, operation!));
        // Assert
        Assert.AreEqual(parameterName, exception.ParamName);
    }

    [TestMethod]
    [DataRow("", "Login")]
    [DataRow("AtariGo.Client", " ")]
    [DataRow("AtariGo\r\nFORGED", "Login")]
    [DataRow("AtariGo.Client", "Login\nFORGED")]
    [DataRow("AtariGo\tClient", "Login")]
    [DataRow("AtariGo.Client", "Login\0FORGED")]
    [DataRow("audit@example.invalid", "Login")]
    [DataRow("AtariGo.Client", "Password=synthetic")]
    [DataRow("AtariGo.Client", "InicioSesión")]
    public void Constructor_UnsafeName_RejectsDefinition(string source, string operation)
    {
        // Arrange
        const int eventId = 1001;
        // Act
        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(
            () => _ = new DiagnosticEventDefinition(eventId, source, operation));
        // Assert
        Assert.IsNotNull(exception.ParamName);
    }

    [TestMethod]
    [DataRow(128, 128)]
    [DataRow(1, 1)]
    public void Constructor_NameAtLengthBoundary_AcceptsDefinition(int sourceLength,
        int operationLength)
    {
        // Arrange
        string source = new string('A', sourceLength);
        string operation = new string('B', operationLength);
        // Act
        var definition = new DiagnosticEventDefinition(1001, source, operation);
        // Assert
        Assert.AreEqual(source, definition.Source);
        Assert.AreEqual(operation, definition.Operation);
    }

    [TestMethod]
    [DataRow(129, 1)]
    [DataRow(1, 129)]
    public void Constructor_OverlongName_RejectsDefinition(int sourceLength, int operationLength)
    {
        // Arrange
        string source = new string('A', sourceLength);
        string operation = new string('B', operationLength);
        // Act
        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(
            () => _ = new DiagnosticEventDefinition(1001, source, operation));
        // Assert
        Assert.IsNotNull(exception.ParamName);
    }
}
