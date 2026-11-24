using System.Diagnostics;
using System.IO;
using System.Text.Json;

using AtariGo.Client.Services;

namespace AtariGo.Client.Tests.Services;

[TestClass]
[TestCategory("Integration")]
[DoNotParallelize]
[OSCondition(OperatingSystems.Windows)]
public sealed class ClientDiagnosticsTests
{
    private readonly TestContext _testContext;

    public ClientDiagnosticsTests(TestContext testContext)
    {
        ArgumentNullException.ThrowIfNull(testContext);
        _testContext = testContext;
    }

    [TestMethod]
    public async Task DisposeAsync_NormalLifetime_DrainsVersionAndRemovesOnlyOwnedListenerAsync()
    {
        // Arrange
        using var directory = new ClientDiagnosticTestDirectory();
        using var unrelated = new DefaultTraceListener();
        Trace.Listeners.Add(unrelated);
        try
        {
            await using var diagnostics = new ClientDiagnostics(directory.Options);
            // Act
            Trace.TraceWarning("Client request failed. Exception type: {0}.", "private");
            await diagnostics.DisposeAsync();
            await diagnostics.DisposeAsync();
            List<JsonElement> entries = directory.ReadEvents();
            // Assert
            Assert.HasCount(3, entries);
            Assert.AreEqual("ClientStarted", entries[0].GetProperty("Operation").GetString());
            Assert.IsNotNull(entries[0].GetProperty("ApplicationVersion").GetString());
            Assert.AreEqual("ClientStopped", entries[^1].GetProperty("Operation").GetString());
            Assert.Contains(unrelated, Trace.Listeners.Cast<TraceListener>());
            Assert.IsEmpty(Trace.Listeners.OfType<ClientFileTraceListener>());
            Assert.AreEqual(0, diagnostics.Status.Pending);
        }
        finally
        {
            Trace.Listeners.Remove(unrelated);
        }
    }

    [TestMethod]
    public async Task FlushAsync_BlockedStorage_ExposesDegradationAndRecoversAsync()
    {
        // Arrange
        using var directory = new ClientDiagnosticTestDirectory();
        string blocked = Path.Combine(directory.DirectoryPath, "blocked");
        await File.WriteAllTextAsync(blocked, "obstacle", _testContext.CancellationToken);
        var options = new AtariGo.Diagnostics.DiagnosticFileOptions(blocked, "client");
        var clock = new RecordingDiagnosticTimeProvider();
        await using var diagnostics = new ClientDiagnostics(options, clock);
        diagnostics.RecordFailure("SyntheticFailure", new IOException("private"));
        // Act
        await clock.DelayScheduled.WaitAsync(_testContext.CancellationToken);
        Assert.IsTrue(diagnostics.Status.IsDegraded);
        Assert.IsNotNull(diagnostics.Status.LastError);
        File.Delete(blocked);
        await diagnostics.FlushAsync(_testContext.CancellationToken);
        // Assert
        Assert.AreEqual(2, diagnostics.Status.Written);
        Assert.IsFalse(diagnostics.Status.IsDegraded);
        Assert.AreEqual(0, diagnostics.Status.Pending);
    }
}
