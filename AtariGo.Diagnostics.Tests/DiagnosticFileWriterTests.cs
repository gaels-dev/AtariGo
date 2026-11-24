using System.Text.Json;

namespace AtariGo.Diagnostics.Tests;

[TestClass]
[TestCategory("Integration")]
[OSCondition(OperatingSystems.Windows)]
public sealed class DiagnosticFileWriterTests
{
    private readonly TestContext _testContext;

    public DiagnosticFileWriterTests(TestContext testContext)
    {
        ArgumentNullException.ThrowIfNull(testContext);
        _testContext = testContext;
    }

    [TestMethod]
    public async Task FlushAsync_ValidEvent_PersistsSafeSingleLineWithUtcOffsetAsync()
    {
        // Arrange
        using var directory = new DiagnosticTestDirectory();
        await using var writer = new DiagnosticFileWriter(directory.Options);
        // Act
        Assert.IsTrue(writer.TryWrite(DiagnosticTestDirectory.CreateEntry(42)));
        await writer.FlushAsync(_testContext.CancellationToken);
        JsonElement entry = directory.ReadEvents().Single();
        // Assert
        Assert.AreEqual(TimeSpan.FromHours(-6), entry.GetProperty("Timestamp")
            .GetDateTimeOffset().Offset);
        Assert.AreEqual(42, entry.GetProperty("GameId").GetInt32());
        Assert.DoesNotContain("synthetic-private-payload", entry.GetRawText(),
            StringComparison.Ordinal);
        Assert.AreEqual(1, writer.Status.Written);
        Assert.AreEqual(0, writer.Status.Pending);
    }

    [TestMethod]
    public async Task DisposeAsync_QueuedEvents_DrainsAndRejectsLateWritesAsync()
    {
        // Arrange
        using var directory = new DiagnosticTestDirectory();
        var writer = new DiagnosticFileWriter(directory.Options);
        Assert.IsTrue(writer.TryWrite(DiagnosticTestDirectory.CreateEntry(1)));
        Assert.IsTrue(writer.TryWrite(DiagnosticTestDirectory.CreateEntry(2)));
        // Act
        await writer.DisposeAsync();
        await writer.DisposeAsync();
        bool late = writer.TryWrite(DiagnosticTestDirectory.CreateEntry(3));
        // Assert
        Assert.IsFalse(late);
        Assert.HasCount(2, directory.ReadEvents());
        Assert.AreEqual(2, writer.Status.Written);
        Assert.AreEqual(0, writer.Status.Unconfirmed);
        Assert.AreEqual(1, writer.Status.Rejected);
    }

    [TestMethod]
    public async Task TryWrite_ConcurrentProducers_PersistsEveryAcceptedEventAsync()
    {
        // Arrange
        using var directory = new DiagnosticTestDirectory();
        await using var writer = new DiagnosticFileWriter(directory.Options);
        // Act
        Parallel.For(1, 129, gameId =>
            Assert.IsTrue(writer.TryWrite(DiagnosticTestDirectory.CreateEntry(gameId))));
        await writer.FlushAsync(_testContext.CancellationToken);
        List<JsonElement> events = directory.ReadEvents();
        // Assert
        Assert.HasCount(128, events);
        Assert.HasCount(128, events.Select(entry => entry.GetProperty("GameId").GetInt32())
            .Distinct());
        Assert.AreEqual(128, writer.Status.Accepted);
    }

    [TestMethod]
    public async Task TryWrite_OverlargeRecord_RejectsWithoutCreatingLogAsync()
    {
        // Arrange
        using var directory = new DiagnosticTestDirectory();
        DiagnosticFileOptions options = directory.Options with { MaximumFileBytes = 1 };
        await using var writer = new DiagnosticFileWriter(options);
        // Act
        bool accepted = writer.TryWrite(DiagnosticTestDirectory.CreateEntry(1));
        await writer.FlushAsync(_testContext.CancellationToken);
        // Assert
        Assert.IsFalse(accepted);
        Assert.IsEmpty(directory.ReadEvents());
        Assert.AreEqual(1, writer.Status.Rejected);
    }

    [TestMethod]
    [Timeout(10000, CooperativeCancellation = true)]
    public async Task TryWrite_UnavailableDirectory_RecoversPendingEventAndReportsDropsAsync()
    {
        // Arrange
        using var directory = new DiagnosticTestDirectory();
        string blocked = Path.Combine(directory.DirectoryPath, "blocked");
        await File.WriteAllTextAsync(blocked, "temporary obstacle", _testContext.CancellationToken);
        var clock = new ControlledDiagnosticTimeProvider();
        DiagnosticFileOptions options = new DiagnosticFileOptions(blocked, "audit")
        {
            QueueCapacity = 1
        };
        var writer = new DiagnosticFileWriter(options, clock);
        await using var cleanup = new DiagnosticWriterCleanup(writer, clock);
        Assert.IsTrue(writer.TryWrite(DiagnosticTestDirectory.CreateEntry(1)));
        ControlledDiagnosticTimer retry = await clock.NextTimerAsync(
            _testContext.CancellationToken);
        // Act
        Assert.IsTrue(writer.TryWrite(DiagnosticTestDirectory.CreateEntry(2)));
        Assert.IsFalse(writer.TryWrite(DiagnosticTestDirectory.CreateEntry(3)));
        Assert.IsTrue(writer.Status.IsDegraded);
        File.Delete(blocked);
        retry.Fire();
        await writer.FlushAsync(_testContext.CancellationToken);
        await writer.DisposeAsync();
        string[] lines = await File.ReadAllLinesAsync(Directory.GetFiles(blocked, "*.log").Single(),
            _testContext.CancellationToken);
        // Assert
        Assert.AreEqual(options.RetryDelay, retry.DueTime);
        Assert.AreEqual(2, writer.Status.Written);
        Assert.AreEqual(1, writer.Status.Rejected);
        Assert.IsFalse(writer.Status.IsDegraded);
        Assert.HasCount(3, lines);
    }

    [TestMethod]
    [Timeout(10000, CooperativeCancellation = true)]
    public async Task DisposeAsync_UnavailableStorage_AccountsPendingEventsAtDeadlineAsync()
    {
        // Arrange
        using var directory = new DiagnosticTestDirectory();
        string blocked = Path.Combine(directory.DirectoryPath, "blocked");
        await File.WriteAllTextAsync(blocked, "temporary obstacle", _testContext.CancellationToken);
        var clock = new ControlledDiagnosticTimeProvider();
        var options = new DiagnosticFileOptions(blocked, "audit");
        var writer = new DiagnosticFileWriter(options, clock);
        await using var cleanup = new DiagnosticWriterCleanup(writer, clock);
        Assert.IsTrue(writer.TryWrite(DiagnosticTestDirectory.CreateEntry(1)));
        await clock.NextTimerAsync(_testContext.CancellationToken);
        // Act
        ValueTask closing = writer.DisposeAsync();
        ControlledDiagnosticTimer deadline = await clock.NextTimerAsync(
            _testContext.CancellationToken);
        deadline.Fire();
        await closing;
        // Assert
        Assert.AreEqual(options.ShutdownTimeout, deadline.DueTime);
        Assert.AreEqual(1, writer.Status.Unconfirmed);
        Assert.AreEqual(0, writer.Status.Pending);
        Assert.IsTrue(writer.Status.IsDegraded);
        Assert.IsNotNull(writer.Status.LastError);
    }

    [TestMethod]
    public async Task TryWrite_FullSegment_RotatesWithoutExceedingSizeAsync()
    {
        // Arrange
        using var directory = new DiagnosticTestDirectory();
        DiagnosticFileOptions options = directory.Options with { MaximumFileBytes = 512 };
        await using var writer = new DiagnosticFileWriter(options);
        // Act
        Assert.IsTrue(writer.TryWrite(DiagnosticTestDirectory.CreateEntry(1)));
        Assert.IsTrue(writer.TryWrite(DiagnosticTestDirectory.CreateEntry(2)));
        await writer.FlushAsync(_testContext.CancellationToken);
        FileInfo[] files = new DirectoryInfo(directory.DirectoryPath).GetFiles("audit-*.log");
        // Assert
        Assert.HasCount(2, files);
        Assert.IsTrue(files.All(file => file.Length <= options.MaximumFileBytes));
        Assert.HasCount(2, directory.ReadEvents());
    }

    [TestMethod]
    public async Task TryWrite_ExpiredOwnedFile_PreservesForeignAndActiveFilesAsync()
    {
        // Arrange
        using var directory = new DiagnosticTestDirectory();
        string expired = Path.Combine(directory.DirectoryPath, $"audit-{Guid.NewGuid():N}.log");
        string foreign = Path.Combine(directory.DirectoryPath, "audit-existing.log");
        await File.WriteAllTextAsync(expired, "expired", _testContext.CancellationToken);
        File.SetLastWriteTimeUtc(expired, DateTime.UnixEpoch);
        await File.WriteAllTextAsync(foreign, "foreign", _testContext.CancellationToken);
        await using var first = new DiagnosticFileWriter(directory.Options);
        Assert.IsTrue(first.TryWrite(DiagnosticTestDirectory.CreateEntry(1)));
        await first.FlushAsync(_testContext.CancellationToken);
        string active = Directory.GetFiles(directory.DirectoryPath, "audit-*.log")
            .Single(file => file != foreign);
        File.SetLastWriteTimeUtc(active, DateTime.UnixEpoch);
        await using var second = new DiagnosticFileWriter(directory.Options);
        // Act
        Assert.IsTrue(second.TryWrite(DiagnosticTestDirectory.CreateEntry(2)));
        await second.FlushAsync(_testContext.CancellationToken);
        // Assert
        Assert.IsFalse(File.Exists(expired));
        Assert.IsTrue(File.Exists(foreign));
        Assert.IsTrue(File.Exists(active));
    }

    [TestMethod]
    public async Task TryWrite_ClosedFilesExceedBudget_PrunesOldSegmentsAsync()
    {
        // Arrange
        using var directory = new DiagnosticTestDirectory();
        DiagnosticFileOptions options = directory.Options with
        {
            MaximumFileBytes = 512,
            MaximumTotalBytes = 512
        };
        // Act
        await using (var first = new DiagnosticFileWriter(options))
        {
            Assert.IsTrue(first.TryWrite(DiagnosticTestDirectory.CreateEntry(1)));
        }
        await using (var second = new DiagnosticFileWriter(options))
        {
            Assert.IsTrue(second.TryWrite(DiagnosticTestDirectory.CreateEntry(2)));
        }
        // Assert
        Assert.HasCount(1, directory.ReadEvents());
        Assert.AreEqual(2, directory.ReadEvents().Single(entry =>
            entry.GetProperty("Operation").GetString() == "GameConnected")
            .GetProperty("GameId").GetInt32());
        Assert.IsLessThanOrEqualTo(options.MaximumTotalBytes,
            new DirectoryInfo(directory.DirectoryPath).GetFiles("*.log").Sum(file => file.Length));
    }

    [TestMethod]
    [Timeout(10000, CooperativeCancellation = true)]
    public async Task TryWrite_BudgetHeldByActiveWriter_RecoversAfterOwnerClosesAsync()
    {
        // Arrange
        using var directory = new DiagnosticTestDirectory();
        DiagnosticFileOptions options = directory.Options with
        {
            MaximumFileBytes = 512,
            MaximumTotalBytes = 512
        };
        await using var first = new DiagnosticFileWriter(options);
        Assert.IsTrue(first.TryWrite(DiagnosticTestDirectory.CreateEntry(1)));
        await first.FlushAsync(_testContext.CancellationToken);
        string active = Directory.GetFiles(directory.DirectoryPath, "*.log").Single();
        var clock = new ControlledDiagnosticTimeProvider();
        var second = new DiagnosticFileWriter(options, clock);
        await using var cleanup = new DiagnosticWriterCleanup(second, clock);
        Assert.IsTrue(second.TryWrite(DiagnosticTestDirectory.CreateEntry(2)));
        ControlledDiagnosticTimer retry = await clock.NextTimerAsync(
            _testContext.CancellationToken);
        Assert.IsTrue(File.Exists(active));
        Assert.IsTrue(second.Status.IsDegraded);
        // Act
        await first.DisposeAsync();
        retry.Fire();
        await second.FlushAsync(_testContext.CancellationToken);
        // Assert
        Assert.IsFalse(File.Exists(active));
        Assert.IsFalse(second.Status.IsDegraded);
        Assert.AreEqual(1, second.Status.Written);
        Assert.AreEqual(2, directory.ReadEvents().Single(entry =>
            entry.GetProperty("Operation").GetString() == "GameConnected")
            .GetProperty("GameId").GetInt32());
    }

    [TestMethod]
    public async Task TryWrite_TwoActiveWriters_SerializesAccessToSharedDirectoryAsync()
    {
        // Arrange
        using var directory = new DiagnosticTestDirectory();
        await using var first = new DiagnosticFileWriter(directory.Options);
        await using var second = new DiagnosticFileWriter(directory.Options);
        // Act
        Parallel.For(1, 129, gameId =>
            Assert.IsTrue((gameId % 2 == 0 ? first : second)
                .TryWrite(DiagnosticTestDirectory.CreateEntry(gameId))));
        await Task.WhenAll(first.FlushAsync(_testContext.CancellationToken),
            second.FlushAsync(_testContext.CancellationToken));
        // Assert
        Assert.AreEqual(128, directory.ReadEvents().Count(entry =>
            entry.GetProperty("Operation").GetString() == "GameConnected"));
        Assert.AreEqual(128, first.Status.Written + second.Status.Written);
    }

    [TestMethod]
    public async Task TryWrite_UnrepresentableLocalTimestamp_RejectsEventAsync()
    {
        // Arrange
        using var directory = new DiagnosticTestDirectory();
        await using var writer = new DiagnosticFileWriter(directory.Options);
        DiagnosticEvent valid = DiagnosticTestDirectory.CreateEntry(1);
        var entry = new DiagnosticEvent(valid.Definition, valid.Details, DateTimeOffset.MinValue);
        // Act
        bool accepted = writer.TryWrite(entry);
        await writer.FlushAsync(_testContext.CancellationToken);
        // Assert
        Assert.IsFalse(accepted);
        Assert.AreEqual(1, writer.Status.Rejected);
        Assert.AreEqual(0, writer.Status.Accepted);
    }

    [TestMethod]
    public async Task FlushAsync_CancelledToken_PropagatesCancellationAsync()
    {
        // Arrange
        using var directory = new DiagnosticTestDirectory();
        await using var writer = new DiagnosticFileWriter(directory.Options);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        // Act
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => writer.FlushAsync(cancellation.Token));
        // Assert
        Assert.AreEqual(0, writer.Status.Accepted);
        Assert.IsFalse(writer.Status.IsDegraded);
    }
}
