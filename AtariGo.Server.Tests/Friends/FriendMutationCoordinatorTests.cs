using AtariGo.Server.Application.Social;

namespace AtariGo.Server.Tests.Friends;

[TestClass]
[TestCategory("Unit")]
public sealed class FriendMutationCoordinatorTests
{
    private static readonly TimeSpan completionTimeout = TimeSpan.FromSeconds(5);

    [TestMethod]
    public async Task DisposeAsync_ActiveAndQueuedLeases_WaitsForBothAndRejectsNewEntries()
    {
        // Arrange
        await using FriendMutationCoordinator coordinator = new FriendMutationCoordinator();
        using FriendMutationLease held = await coordinator.EnterAsync(CancellationToken.None);
        Task<FriendMutationLease> queued = coordinator.EnterAsync(CancellationToken.None);
        // Act
        coordinator.Dispose();
        Task closing = coordinator.DisposeAsync().AsTask();
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => coordinator.EnterAsync(CancellationToken.None));
        Assert.IsFalse(closing.IsCompleted);
        held.Dispose();
        held.Dispose();
        using FriendMutationLease next = await queued.WaitAsync(completionTimeout);
        Assert.IsFalse(closing.IsCompleted);
        next.Dispose();
        next.Dispose();
        await closing.WaitAsync(completionTimeout);
        // Assert
        Assert.IsTrue(closing.IsCompletedSuccessfully);
    }

    [TestMethod]
    public async Task DisposeAsync_CancelledWaiter_WaitsOnlyForActiveLease()
    {
        // Arrange
        await using FriendMutationCoordinator coordinator = new FriendMutationCoordinator();
        using FriendMutationLease held = await coordinator.EnterAsync(CancellationToken.None);
        using CancellationTokenSource cancellation = new CancellationTokenSource();
        Task<FriendMutationLease> queued = coordinator.EnterAsync(cancellation.Token);
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
    public async Task EnterAsync_CancelledWaiter_DoesNotReleaseAnotherLeasesPermit()
    {
        // Arrange
        await using FriendMutationCoordinator coordinator = new FriendMutationCoordinator();
        using FriendMutationLease held = await coordinator.EnterAsync(CancellationToken.None);
        using CancellationTokenSource cancellation = new CancellationTokenSource();
        Task<FriendMutationLease> cancelled = coordinator.EnterAsync(cancellation.Token);
        // Act
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => cancelled.WaitAsync(completionTimeout));
        Task<FriendMutationLease> next = coordinator.EnterAsync(CancellationToken.None);
        bool waitedForOwner = !next.IsCompleted;
        held.Dispose();
        using FriendMutationLease acquired = await next.WaitAsync(completionTimeout);
        // Assert
        Assert.IsTrue(waitedForOwner);
    }
}
