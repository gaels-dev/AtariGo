using System.Diagnostics;
using System.IO;

using AtariGo.Client.Commands;
using AtariGo.Client.Services;
using AtariGo.Client.Tests.Services;

namespace AtariGo.Client.Tests.Commands;

[TestClass]
[TestCategory("Integration")]
[DoNotParallelize]
[OSCondition(OperatingSystems.Windows)]
public sealed class AsyncRelayCommandTests
{
    private readonly TestContext _testContext;

    public AsyncRelayCommandTests(TestContext testContext)
    {
        ArgumentNullException.ThrowIfNull(testContext);
        _testContext = testContext;
    }

    [TestMethod]
    public async Task Execute_OwnerCancellation_RecordsExpectedCancellationAsync()
    {
        // Arrange
        using var directory = new ClientDiagnosticTestDirectory();
        await using var diagnostics = new ClientDiagnostics(directory.Options);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        int failures = 0;
        var command = new AsyncRelayCommand(_ => Task.FromCanceled(cancellation.Token),
            _ => failures++, cancellation.Token);
        // Act
        await ExecuteAsync(command);
        await diagnostics.FlushAsync(_testContext.CancellationToken);
        // Assert
        Assert.AreEqual(0, failures);
        Assert.IsTrue(command.CanExecute(null));
        Assert.AreEqual("AsyncCommandCancelledByItsOwner", directory.ReadEvents()[1]
            .GetProperty("Operation").GetString());
        Assert.AreEqual("Information", directory.ReadEvents()[1]
            .GetProperty("Severity").GetString());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Execute_FailureDuringCancellation_InvokesResponsibleHandlerOnceAsync(
        bool foreignCancellation)
    {
        // Arrange
        using var directory = new ClientDiagnosticTestDirectory();
        await using var diagnostics = new ClientDiagnostics(directory.Options);
        using var owner = new CancellationTokenSource();
        using var foreign = new CancellationTokenSource();
        await owner.CancelAsync();
        await foreign.CancelAsync();
        Exception failure = foreignCancellation
            ? new OperationCanceledException(foreign.Token) : new InvalidOperationException();
        int failures = 0;
        void HandleFailure(Exception exception)
        {
            failures++;
            Trace.TraceWarning("Gameplay command failed: {0}.", exception.GetType().Name);
        }
        var command = new AsyncRelayCommand(_ => Task.FromException(failure), HandleFailure,
            owner.Token);
        // Act
        await ExecuteAsync(command);
        await diagnostics.FlushAsync(_testContext.CancellationToken);
        // Assert
        Assert.AreEqual(1, failures);
        Assert.IsTrue(command.CanExecute(null));
        Assert.HasCount(2, directory.ReadEvents());
        Assert.AreEqual("GameplayCommandFailed", directory.ReadEvents()[1]
            .GetProperty("Operation").GetString());
    }

    [TestMethod]
    public async Task Execute_FailureHandlerThrows_RecordsDistinctHandlerFailureAsync()
    {
        // Arrange
        using var directory = new ClientDiagnosticTestDirectory();
        await using var diagnostics = new ClientDiagnostics(directory.Options);
        var command = new AsyncRelayCommand(_ => Task.FromException(new IOException()),
            _ => throw new InvalidOperationException());
        // Act
        await ExecuteAsync(command);
        await diagnostics.FlushAsync(_testContext.CancellationToken);
        // Assert
        Assert.IsTrue(command.CanExecute(null));
        Assert.HasCount(2, directory.ReadEvents());
        Assert.AreEqual("AsyncCommandFailureHandlerFailedExceptionTypes",
            directory.ReadEvents()[1].GetProperty("Operation").GetString());
    }

    private async Task ExecuteAsync(AsyncRelayCommand command)
    {
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnStateChanged(object? sender, EventArgs eventArgs)
        {
            if (command.CanExecute(null))
            {
                finished.TrySetResult();
            }
        }
        command.CanExecuteChanged += OnStateChanged;
        try
        {
            command.Execute(null);
            await finished.Task.WaitAsync(_testContext.CancellationToken);
        }
        finally
        {
            command.CanExecuteChanged -= OnStateChanged;
        }
    }
}
