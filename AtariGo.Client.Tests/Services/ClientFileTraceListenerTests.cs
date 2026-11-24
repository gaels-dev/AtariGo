using System.Diagnostics;
using System.Text.Json;

using AtariGo.Client.Services;
using AtariGo.Diagnostics;

namespace AtariGo.Client.Tests.Services;

[TestClass]
[TestCategory("Integration")]
[OSCondition(OperatingSystems.Windows)]
public sealed class ClientFileTraceListenerTests
{
    private readonly TestContext _testContext;

    public ClientFileTraceListenerTests(TestContext testContext)
    {
        ArgumentNullException.ThrowIfNull(testContext);
        _testContext = testContext;
    }

    [TestMethod]
    public async Task TraceEvent_RegisteredTemplate_PreservesIdentityWithoutPrivateArgumentsAsync()
    {
        // Arrange
        using var directory = new ClientDiagnosticTestDirectory();
        await using var writer = new DiagnosticFileWriter(directory.Options);
        using var listener = new ClientFileTraceListener(writer);
        // Act
        listener.TraceEvent(null, "Test.Source", TraceEventType.Warning, 42,
            "Client request failed. Exception type: {0}.", "private\r\npayload");
        await writer.FlushAsync(_testContext.CancellationToken);
        JsonElement entry = directory.ReadEvents().Single();
        // Assert
        Assert.AreEqual("Test.Source", entry.GetProperty("Source").GetString());
        Assert.AreEqual(42, entry.GetProperty("TraceEventId").GetInt32());
        Assert.AreEqual("Warning", entry.GetProperty("Severity").GetString());
        Assert.AreEqual("ClientRequestFailedExceptionType",
            entry.GetProperty("Operation").GetString());
        Assert.DoesNotContain("private", entry.GetRawText(), StringComparison.Ordinal);
        Assert.AreEqual(TimeSpan.FromHours(-6),
            entry.GetProperty("Timestamp").GetDateTimeOffset().Offset);
    }

    [TestMethod]
    public async Task TraceEvent_LevelFilter_RejectsInformationAndAcceptsErrorAsync()
    {
        // Arrange
        using var directory = new ClientDiagnosticTestDirectory();
        await using var writer = new DiagnosticFileWriter(directory.Options);
        using var listener = new ClientFileTraceListener(writer)
        {
            Filter = new EventTypeFilter(SourceLevels.Error)
        };
        // Act
        listener.TraceEvent(null, "Test", TraceEventType.Information, 0, "filtered");
        listener.TraceEvent(null, "Test", TraceEventType.Error, 0, "accepted");
        await writer.FlushAsync(_testContext.CancellationToken);
        // Assert
        Assert.HasCount(1, directory.ReadEvents());
        Assert.AreEqual("Error", directory.ReadEvents().Single()
            .GetProperty("Severity").GetString());
        Assert.AreEqual(1, listener.Status.Written);
    }

    [TestMethod]
    [DataRow("Broken {", 1)]
    [DataRow("Missing {2}", 1)]
    public async Task TraceEvent_InvalidTemplate_RecordsControlledFailureAsync(string format,
        int value)
    {
        // Arrange
        using var directory = new ClientDiagnosticTestDirectory();
        await using var writer = new DiagnosticFileWriter(directory.Options);
        using var listener = new ClientFileTraceListener(writer);
        // Act
        listener.TraceEvent(null, "Test", TraceEventType.Warning, 0, format, value);
        await writer.FlushAsync(_testContext.CancellationToken);
        // Assert
        Assert.AreEqual("InvalidTraceFormat", directory.ReadEvents().Single()
            .GetProperty("Operation").GetString());
    }

    [TestMethod]
    public async Task TraceEvent_RawException_OnlyPersistsSafeErrorAsync()
    {
        // Arrange
        using var directory = new ClientDiagnosticTestDirectory();
        await using var writer = new DiagnosticFileWriter(directory.Options);
        using var listener = new ClientFileTraceListener(writer);
        // Act
        listener.TraceEvent(null, "unsafe\r\nsource", TraceEventType.Critical, -4,
            "private template {0}", new InvalidOperationException("private-payload"));
        listener.WriteLine("private raw text");
        await writer.FlushAsync(_testContext.CancellationToken);
        List<JsonElement> entries = directory.ReadEvents();
        // Assert
        Assert.HasCount(2, entries);
        Assert.AreEqual("AtariGo.Client.UnknownSource",
            entries[0].GetProperty("Source").GetString());
        Assert.AreEqual(-4, entries[0].GetProperty("TraceEventId").GetInt32());
        Assert.AreEqual("InvalidOperationException", entries[0]
            .GetProperty("Error").GetProperty("TypeName").GetString());
        Assert.IsTrue(entries.All(entry =>
            !entry.GetRawText().Contains("private", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task TraceData_TypedEvent_RespectsFilterAndKeepsContextAsync()
    {
        // Arrange
        using var directory = new ClientDiagnosticTestDirectory();
        await using var writer = new DiagnosticFileWriter(directory.Options);
        using var listener = new ClientFileTraceListener(writer)
        {
            Filter = new EventTypeFilter(SourceLevels.Error)
        };
        var definition = new DiagnosticEventDefinition(1002, "Test", "TypedFailure");
        var details = new DiagnosticEventDetails
        {
            Severity = DiagnosticSeverity.Error,
            Context = new DiagnosticContext { GameId = 31 }
        };
        var entry = new DiagnosticEvent(definition, details, DateTimeOffset.UtcNow);
        // Act
        listener.TraceData(null, "Test", TraceEventType.Information, 1002, entry);
        listener.TraceData(null, "Test", TraceEventType.Error, 1002, entry);
        listener.TraceData(null, "Test", TraceEventType.Information, 1002,
            new object?[] { entry });
        listener.TraceData(null, "Test", TraceEventType.Error, 1002,
            new object?[] { "private", entry });
        listener.TraceData(null, "Test", TraceEventType.Error, 1002, "private");
        await writer.FlushAsync(_testContext.CancellationToken);
        // Assert
        Assert.HasCount(2, directory.ReadEvents());
        Assert.IsTrue(directory.ReadEvents().All(item =>
            item.GetProperty("GameId").GetInt32() == 31));
    }

    [TestMethod]
    [DataRow(TraceEventType.Error, "Error")]
    [DataRow(TraceEventType.Critical, "Critical")]
    [DataRow(TraceEventType.Verbose, "Debug")]
    public async Task TraceEvent_Severity_PreservesLevelAsync(TraceEventType severity,
        string expected)
    {
        // Arrange
        using var directory = new ClientDiagnosticTestDirectory();
        await using var writer = new DiagnosticFileWriter(directory.Options);
        using var listener = new ClientFileTraceListener(writer);
        // Act
        listener.TraceEvent(null, "Test", severity, 0, null);
        await writer.FlushAsync(_testContext.CancellationToken);
        // Assert
        Assert.AreEqual(expected, directory.ReadEvents().Single()
            .GetProperty("Severity").GetString());
    }

    [TestMethod]
    [DataRow("InvalidOperationException")]
    [DataRow("RpcException")]
    public async Task TraceEvent_KnownExceptionName_PreservesTypeWithoutInventingCodeAsync(
        string typeName)
    {
        // Arrange
        using var directory = new ClientDiagnosticTestDirectory();
        await using var writer = new DiagnosticFileWriter(directory.Options);
        using var listener = new ClientFileTraceListener(writer);
        // Act
        listener.TraceEvent(null, "Test", TraceEventType.Warning, 0,
            "Client request failed. Exception type: {0}.", typeName);
        await writer.FlushAsync(_testContext.CancellationToken);
        JsonElement error = directory.ReadEvents().Single().GetProperty("Error");
        // Assert
        Assert.AreEqual(typeName, error.GetProperty("TypeName").GetString());
        Assert.AreEqual(JsonValueKind.Null, error.GetProperty("HResult").ValueKind);
    }
}
