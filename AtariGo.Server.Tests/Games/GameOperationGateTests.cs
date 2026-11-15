using AtariGo.Server.Application.Games;

namespace AtariGo.Server.Tests.Games;

[TestClass]
[TestCategory("Unit")]
public sealed class GameOperationGateTests
{
    private static readonly TimeSpan completionTimeout = TimeSpan.FromSeconds(5);

    [TestMethod]
    public async Task EnterAsync_SameGame_WaitsUntilLeaseIsReleased()
    {
        // Arrange
        await using GameOperationGate gate = new GameOperationGate();
        using GameOperationLease active = await gate.EnterAsync(1, CancellationToken.None);
        // Act
        Task<GameOperationLease> pending = gate.EnterAsync(1, CancellationToken.None);
        bool wasWaiting = !pending.IsCompleted;
        active.Dispose();
        using GameOperationLease next = await pending.WaitAsync(completionTimeout);
        // Assert
        Assert.IsTrue(wasWaiting);
    }

    [TestMethod]
    public async Task EnterAsync_IndependentGames_AcquiresBothLeases()
    {
        // Arrange
        await using GameOperationGate gate = new GameOperationGate();
        using GameOperationLease first = await gate.EnterAsync(1, CancellationToken.None);
        // Act
        using GameOperationLease second = await gate.EnterAsync(2, CancellationToken.None)
            .WaitAsync(completionTimeout);
        // Assert
        Assert.IsNotNull(second);
    }

    [TestMethod]
    public async Task EnterAsync_CollidingGames_SharesTheSameGate()
    {
        // Arrange
        await using GameOperationGate gate = new GameOperationGate();
        using GameOperationLease active = await gate.EnterAsync(1, CancellationToken.None);
        // Act
        Task<GameOperationLease> pending = gate.EnterAsync(129, CancellationToken.None);
        bool wasWaiting = !pending.IsCompleted;
        active.Dispose();
        using GameOperationLease next = await pending.WaitAsync(completionTimeout);
        // Assert
        Assert.IsTrue(wasWaiting);
    }

    [TestMethod]
    public async Task EnterAsync_CancelledWait_DoesNotReleaseAnActiveLease()
    {
        // Arrange
        await using GameOperationGate gate = new GameOperationGate();
        using GameOperationLease active = await gate.EnterAsync(1, CancellationToken.None);
        using CancellationTokenSource cancellation = new CancellationTokenSource();
        Task<GameOperationLease> cancelled = gate.EnterAsync(1, cancellation.Token);
        // Act
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => cancelled);
        Task<GameOperationLease> pending = gate.EnterAsync(1, CancellationToken.None);
        bool wasWaiting = !pending.IsCompleted;
        active.Dispose();
        using GameOperationLease next = await pending.WaitAsync(completionTimeout);
        // Assert
        Assert.IsTrue(wasWaiting);
    }

    [TestMethod]
    public async Task Dispose_RepeatedLeaseDisposal_ReleasesOnlyOnce()
    {
        // Arrange
        await using GameOperationGate gate = new GameOperationGate();
        GameOperationLease first = await gate.EnterAsync(1, CancellationToken.None);
        first.Dispose();
        using GameOperationLease second = await gate.EnterAsync(1, CancellationToken.None);
        // Act
        Parallel.Invoke(first.Dispose, first.Dispose);
        Task<GameOperationLease> pending = gate.EnterAsync(1, CancellationToken.None);
        bool wasWaiting = !pending.IsCompleted;
        second.Dispose();
        using GameOperationLease third = await pending.WaitAsync(completionTimeout);
        // Assert
        Assert.IsTrue(wasWaiting);
    }

    [TestMethod]
    public async Task DisposeAsync_AdmittedWaiter_DrainsBeforeClosing()
    {
        // Arrange
        await using GameOperationGate gate = new GameOperationGate();
        using GameOperationLease active = await gate.EnterAsync(1, CancellationToken.None);
        Task<GameOperationLease> pending = gate.EnterAsync(1, CancellationToken.None);
        // Act
        Task closing = gate.DisposeAsync().AsTask();
        bool waitedForActive = !closing.IsCompleted;
        active.Dispose();
        using GameOperationLease next = await pending.WaitAsync(completionTimeout);
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
    public async Task DisposeAsync_CancelledWaiter_DoesNotPreventClosing()
    {
        // Arrange
        await using GameOperationGate gate = new GameOperationGate();
        using GameOperationLease active = await gate.EnterAsync(1, CancellationToken.None);
        using CancellationTokenSource cancellation = new CancellationTokenSource();
        Task<GameOperationLease> pending = gate.EnterAsync(1, cancellation.Token);
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
