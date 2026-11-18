using AtariGo.Server.Concurrency;
using AtariGo.Server.Application.Social;

namespace AtariGo.Server.Tests.Friends;

[TestClass]
[TestCategory("Unit")]
public sealed class FriendMutationCoordinatorTests
{
    private static readonly TimeSpan completionTimeout = TimeSpan.FromSeconds(5);

    [TestMethod]
    public async Task DisposeAsync_ActiveAndQueuedLeases_WaitsForBothAndRejectsNewEntriesAsync()
    {
        // Arrange
        await using FriendMutationCoordinator coordinator = new FriendMutationCoordinator();
        using AsyncOperationLease held = await coordinator.EnterAsync(CancellationToken.None);
        Task<AsyncOperationLease> queued = coordinator.EnterAsync(CancellationToken.None);
        // Act
        coordinator.Dispose();
        Task closing = coordinator.DisposeAsync().AsTask();
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => coordinator.EnterAsync(CancellationToken.None));
        Assert.IsFalse(closing.IsCompleted);
        held.Dispose();
        held.Dispose();
        using AsyncOperationLease next = await queued.WaitAsync(completionTimeout);
        Assert.IsFalse(closing.IsCompleted);
        next.Dispose();
        next.Dispose();
        await closing.WaitAsync(completionTimeout);
        // Assert
        Assert.IsTrue(closing.IsCompletedSuccessfully);
    }

    [TestMethod]
    public async Task DisposeAsync_CancelledWaiter_WaitsOnlyForActiveLeaseAsync()
    {
        // Arrange
        await using FriendMutationCoordinator coordinator = new FriendMutationCoordinator();
        using AsyncOperationLease held = await coordinator.EnterAsync(CancellationToken.None);
        using CancellationTokenSource cancellation = new CancellationTokenSource();
        Task<AsyncOperationLease> queued = coordinator.EnterAsync(cancellation.Token);
        // Act
        Task closing = coordinator.DisposeAsync().AsTask();
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => queued.WaitAsync(completionTimeout));
        Assert.IsFalse(closing.IsCompleted);
        held.Dispose();
        await closing.WaitAsync(completionTimeout);
        // Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => coordinator.EnterAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task EnterAsync_CancelledWaiter_DoesNotReleaseAnotherLeasesPermitAsync()
    {
        // Arrange
        await using FriendMutationCoordinator coordinator = new FriendMutationCoordinator();
        using AsyncOperationLease held = await coordinator.EnterAsync(CancellationToken.None);
        using CancellationTokenSource cancellation = new CancellationTokenSource();
        Task<AsyncOperationLease> cancelled = coordinator.EnterAsync(cancellation.Token);
        // Act
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => cancelled.WaitAsync(completionTimeout));
        Task<AsyncOperationLease> next = coordinator.EnterAsync(CancellationToken.None);
        bool waitedForOwner = !next.IsCompleted;
        held.Dispose();
        using AsyncOperationLease acquired = await next.WaitAsync(completionTimeout);
        // Assert
        Assert.IsTrue(waitedForOwner);
    }
}
