using AtariGo.Server.Concurrency;
using AtariGo.Server.Application.Games;

namespace AtariGo.Server.Tests.Games;

[TestClass]
[TestCategory("Unit")]
public sealed class GameOperationGateTests
{
    private static readonly TimeSpan completionTimeout = TimeSpan.FromSeconds(5);

    [TestMethod]
    public async Task EnterAsync_SameGame_WaitsUntilLeaseIsReleasedAsync()
    {
        // Arrange
        await using GameOperationGate gate = new GameOperationGate();
        using AsyncOperationLease active = await gate.EnterAsync(1, CancellationToken.None);
        // Act
        Task<AsyncOperationLease> pending = gate.EnterAsync(1, CancellationToken.None);
        bool wasWaiting = !pending.IsCompleted;
        active.Dispose();
        using AsyncOperationLease next = await pending.WaitAsync(completionTimeout);
        // Assert
        Assert.IsTrue(wasWaiting);
    }

    [TestMethod]
    public async Task EnterAsync_IndependentGames_AcquiresBothLeasesAsync()
    {
        // Arrange
        await using GameOperationGate gate = new GameOperationGate();
        using AsyncOperationLease first = await gate.EnterAsync(1, CancellationToken.None);
        // Act
        using AsyncOperationLease second = await gate.EnterAsync(2, CancellationToken.None)
            .WaitAsync(completionTimeout);
        // Assert
        Assert.IsNotNull(second);
    }

    [TestMethod]
    public async Task EnterAsync_CollidingGames_SharesTheSameGateAsync()
    {
        // Arrange
        await using GameOperationGate gate = new GameOperationGate();
        using AsyncOperationLease active = await gate.EnterAsync(1, CancellationToken.None);
        // Act
        Task<AsyncOperationLease> pending = gate.EnterAsync(129, CancellationToken.None);
        bool wasWaiting = !pending.IsCompleted;
        active.Dispose();
        using AsyncOperationLease next = await pending.WaitAsync(completionTimeout);
        // Assert
        Assert.IsTrue(wasWaiting);
    }

    [TestMethod]
    public async Task EnterAsync_CancelledWait_DoesNotReleaseAnActiveLeaseAsync()
    {
        // Arrange
        await using GameOperationGate gate = new GameOperationGate();
        using AsyncOperationLease active = await gate.EnterAsync(1, CancellationToken.None);
        using CancellationTokenSource cancellation = new CancellationTokenSource();
        Task<AsyncOperationLease> cancelled = gate.EnterAsync(1, cancellation.Token);
        // Act
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => cancelled);
        Task<AsyncOperationLease> pending = gate.EnterAsync(1, CancellationToken.None);
        bool wasWaiting = !pending.IsCompleted;
        active.Dispose();
        using AsyncOperationLease next = await pending.WaitAsync(completionTimeout);
        // Assert
        Assert.IsTrue(wasWaiting);
    }

    [TestMethod]
    public async Task Dispose_RepeatedLeaseDisposal_ReleasesOnlyOnceAsync()
    {
        // Arrange
        await using GameOperationGate gate = new GameOperationGate();
        AsyncOperationLease first = await gate.EnterAsync(1, CancellationToken.None);
        first.Dispose();
        using AsyncOperationLease second = await gate.EnterAsync(1, CancellationToken.None);
        // Act
        Parallel.Invoke(first.Dispose, first.Dispose);
        Task<AsyncOperationLease> pending = gate.EnterAsync(1, CancellationToken.None);
        bool wasWaiting = !pending.IsCompleted;
        second.Dispose();
        using AsyncOperationLease third = await pending.WaitAsync(completionTimeout);
        // Assert
        Assert.IsTrue(wasWaiting);
    }

    [TestMethod]
    public async Task DisposeAsync_AdmittedWaiter_DrainsBeforeClosingAsync()
    {
        // Arrange
        await using GameOperationGate gate = new GameOperationGate();
        using AsyncOperationLease active = await gate.EnterAsync(1, CancellationToken.None);
        Task<AsyncOperationLease> pending = gate.EnterAsync(1, CancellationToken.None);
        // Act
        Task closing = gate.DisposeAsync().AsTask();
        bool waitedForActive = !closing.IsCompleted;
        active.Dispose();
        using AsyncOperationLease next = await pending.WaitAsync(completionTimeout);
        bool waitedForPending = !closing.IsCompleted;
        next.Dispose();
        await closing.WaitAsync(completionTimeout);
        // Assert
        Assert.IsTrue(waitedForActive);
        Assert.IsTrue(waitedForPending);
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            gate.EnterAsync(1, CancellationToken.None));
        await gate.DisposeAsync();
    }

    [TestMethod]
    public async Task DisposeAsync_CancelledWaiter_DoesNotPreventClosingAsync()
    {
        // Arrange
        await using GameOperationGate gate = new GameOperationGate();
        using AsyncOperationLease active = await gate.EnterAsync(1, CancellationToken.None);
        using CancellationTokenSource cancellation = new CancellationTokenSource();
        Task<AsyncOperationLease> pending = gate.EnterAsync(1, cancellation.Token);
        // Act
        Task closing = gate.DisposeAsync().AsTask();
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => pending);
        bool waitedForActive = !closing.IsCompleted;
        active.Dispose();
        await closing.WaitAsync(completionTimeout);
        // Assert
        Assert.IsTrue(waitedForActive);
    }
}
