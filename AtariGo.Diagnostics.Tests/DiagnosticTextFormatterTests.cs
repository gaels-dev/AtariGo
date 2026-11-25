using System.Globalization;
using System.Text;

namespace AtariGo.Diagnostics.Tests;

[TestClass]
[TestCategory("Unit")]
public sealed class DiagnosticTextFormatterTests
{
    [TestMethod]
    public void Format_Startup_WritesTimestampLevelAndReadableMessage()
    {
        // Arrange
        var definition = new DiagnosticEventDefinition(1000, "AtariGo.Client", "ClientStarted");
        var timestamp = new DateTimeOffset(2026, 11, 25, 18, 0, 0, TimeSpan.Zero);
        var entry = new DiagnosticEvent(definition, new DiagnosticEventDetails(), timestamp);
        // Act
        string line = Encoding.UTF8.GetString(DiagnosticFileFormatter.Format(entry));
        // Assert
        Assert.StartsWith("2026-11-25T12:00:00.0000000-06:00 Information: "
            + "Client diagnostics started.", line, StringComparison.Ordinal);
        Assert.EndsWith("[Source=AtariGo.Client; Id=1000; Operation=ClientStarted]\n", line,
            StringComparison.Ordinal);
        Assert.DoesNotContain("null", line, StringComparison.Ordinal);
        Assert.DoesNotContain('{', line);
        Assert.ContainsSingle(character => character == '\n', line);
    }

    [TestMethod]
    public void Format_ContextAndError_PreservesSafeDataWithoutPrivateMessage()
    {
        // Arrange
        var definition = new DiagnosticEventDefinition(5010, "AtariGo.Server.Game",
            "ActionCommitted");
        var details = new DiagnosticEventDetails
        {
            Outcome = DiagnosticOutcome.Succeeded,
            Context = new DiagnosticContext { GameId = 42, ActionId = Guid.Empty },
            Error = DiagnosticError.FromException(new IOException("private\nforged-entry"), 32)
        };
        var entry = new DiagnosticEvent(definition, details, DateTimeOffset.UnixEpoch);
        // Act
        string line = Encoding.UTF8.GetString(DiagnosticFileFormatter.Format(entry));
        // Assert
        Assert.Contains("Action committed. Exception type: IOException.", line,
            StringComparison.Ordinal);
        Assert.Contains("GameId=42", line, StringComparison.Ordinal);
        Assert.Contains("NativeCode=32", line, StringComparison.Ordinal);
        Assert.DoesNotContain("private", line, StringComparison.Ordinal);
        Assert.DoesNotContain("RoomId", line, StringComparison.Ordinal);
    }

    [TestMethod]
    [DataRow("https://localhost:7061")]
    [DataRow("https://[::]:7062")]
    public void Format_ServerListening_PreservesValidatedAddress(string address)
    {
        // Arrange
        var definition = new DiagnosticEventDefinition(14, "Microsoft.Hosting.Lifetime",
            "ServerListening");
        var details = new DiagnosticEventDetails
        {
            Host = new DiagnosticHostContext(address, null)
        };
        var entry = new DiagnosticEvent(definition, details, DateTimeOffset.UnixEpoch);
        // Act
        string message = DiagnosticConsoleFormatter.Format(entry);
        // Assert
        Assert.AreEqual("Now listening on: " + address, message);
    }

    [TestMethod]
    [DataRow("https://private:secret@localhost:7061")]
    [DataRow("https://localhost:7061/private")]
    [DataRow("https://localhost:7061?token=private")]
    [DataRow("https://localhost:7061#private")]
    [DataRow("https://localhost:7061\r\nprivate")]
    [DataRow("file:///C:/private")]
    public void Constructor_UnsafeHostData_DiscardsValues(string address)
    {
        // Arrange
        const string environmentName = "private-environment";
        // Act
        var host = new DiagnosticHostContext(address, environmentName);
        // Assert
        Assert.IsNull(host.ListeningAddress);
        Assert.IsNull(host.EnvironmentName);
    }

    [TestMethod]
    public void Format_NonEnglishCulture_UsesStableTechnicalNumbers()
    {
        // Arrange
        CultureInfo previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
        try
        {
            // Act
            string line = Encoding.UTF8.GetString(DiagnosticFileFormatter.Format(
                DiagnosticTestDirectory.CreateEntry(42)));
            // Assert
            Assert.StartsWith("1969-12-31T18:00:00.0000000-06:00 Information:", line,
                StringComparison.Ordinal);
            Assert.Contains("GameId=42", line, StringComparison.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
