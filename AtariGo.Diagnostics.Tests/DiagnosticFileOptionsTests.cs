namespace AtariGo.Diagnostics.Tests;

[TestClass]
[TestCategory("Unit")]
public sealed class DiagnosticFileOptionsTests
{
    [TestMethod]
    [DataRow("../audit")]
    [DataRow("audit*")]
    [DataRow("audit.log")]
    [DataRow("audit\nforged")]
    [DataRow(" ")]
    public void Constructor_UnsafePrefix_RejectsOptions(string prefix)
    {
        // Arrange
        string directory = Path.GetTempPath();
        // Act
        ArgumentException error = Assert.ThrowsExactly<ArgumentException>(
            () => _ = new DiagnosticFileOptions(directory, prefix));
        // Assert
        Assert.AreEqual("filePrefix", error.ParamName);
    }

    [TestMethod]
    [DataRow("File")]
    [DataRow("Total")]
    [DataRow("Queue")]
    [DataRow("Retention")]
    [DataRow("Retry")]
    [DataRow("Shutdown")]
    [DataRow("Overflow")]
    public void Constructor_InvalidStorageLimit_RejectsWriter(string option)
    {
        // Arrange
        var defaults = new DiagnosticFileOptions(Path.GetTempPath(), "audit");
        DiagnosticFileOptions options = option switch
        {
            "File" => defaults with { MaximumFileBytes = 0 },
            "Total" => defaults with { MaximumTotalBytes = 1 },
            "Queue" => defaults with { QueueCapacity = 0 },
            "Retention" => defaults with { Retention = TimeSpan.Zero },
            "Retry" => defaults with { RetryDelay = TimeSpan.Zero },
            "Shutdown" => defaults with { ShutdownTimeout = TimeSpan.Zero },
            _ => defaults with { RetryDelay = TimeSpan.MaxValue }
        };
        // Act
        ArgumentOutOfRangeException error = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => _ = new DiagnosticFileWriter(options));
        // Assert
        Assert.IsNotNull(error.ParamName);
    }
}
