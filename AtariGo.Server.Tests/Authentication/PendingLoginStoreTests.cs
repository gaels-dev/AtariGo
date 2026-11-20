using Microsoft.Extensions.Options;

using AtariGo.Server.Concurrency;
using AtariGo.Server.Application.Authentication;
using AtariGo.Server.Tests.RoomInvitations;

namespace AtariGo.Server.Tests.Authentication;

[TestClass]
[TestCategory("Unit")]
public sealed class PendingLoginStoreTests
{
    private static readonly TimeSpan completionTimeout = TimeSpan.FromSeconds(5);

    [TestMethod]
    public void TryAdd_AtCapacity_PreservesExistingAttemptAndRejectsAnother()
    {
        var clock = new ManualTimeProvider();
        using var store = new PendingLoginStore(clock, Options.Create(new LoginVerificationOptions
        {
            MaximumPendingLogins = 1
        }));
        PendingLogin first = CreatePending(clock, "first");
        bool added = store.TryAdd(first);
        bool full = store.TryAdd(CreatePending(clock, "second"));
        Assert.IsTrue(added);
        Assert.IsFalse(full);
        Assert.AreEqual(first, store.Find("first"));
        Assert.IsNull(store.Find("second"));
    }

    [TestMethod]
    public void TryAdd_ExpiredAttemptAtCapacity_RemovesItAndAcceptsNewAttempt()
    {
        var clock = new ManualTimeProvider();
        using var store = new PendingLoginStore(clock, Options.Create(new LoginVerificationOptions
        {
            MaximumPendingLogins = 1
        }));
        store.TryAdd(CreatePending(clock, "first"));
        clock.Advance(LoginVerificationOptions.IssuanceLifetime
            + LoginVerificationOptions.CodeLifetime);
        bool added = store.TryAdd(CreatePending(clock, "second"));
        Assert.IsTrue(added);
        Assert.IsNull(store.Find("first"));
        Assert.IsNotNull(store.Find("second"));
    }

    [TestMethod]
    public void Publish_ClosedOrMissingAttempt_RemovesItWithoutRestoringOldState()
    {
        var clock = new ManualTimeProvider();
        using var store = new PendingLoginStore(clock,
            Options.Create(new LoginVerificationOptions()));
        PendingLogin pending = CreatePending(clock, "first");
        store.TryAdd(pending);
        store.Publish(pending with
        {
            IsClosed = true
        });
        store.Publish(pending with
        {
            DeliveryStatus = LoginDeliveryStatus.Sent
        });
        Assert.IsNull(store.Find("first"));
    }

    [TestMethod]
    public async Task EnterAsync_CancelledWaiter_DoesNotReleaseAnotherOperationsPermitAsync()
    {
        await using var store = new PendingLoginStore(TimeProvider.System,
            Options.Create(new LoginVerificationOptions()));
        using AsyncOperationLease held = await store.EnterAsync("first", CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        Task<AsyncOperationLease> cancelled = store.EnterAsync("first", cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => cancelled.WaitAsync(completionTimeout));
        Task<AsyncOperationLease> next = store.EnterAsync("first", CancellationToken.None);
        bool waitedForOwner = !next.IsCompleted;
        held.Dispose();
        using AsyncOperationLease acquired = await next.WaitAsync(completionTimeout);
        Assert.IsTrue(waitedForOwner);
    }

    [TestMethod]
    public async Task DisposeAsync_ActiveAndQueuedOperations_WaitsAndRejectsNewOperationsAsync()
    {
        await using var store = new PendingLoginStore(TimeProvider.System,
            Options.Create(new LoginVerificationOptions()));
        using AsyncOperationLease held = await store.EnterAsync("first", CancellationToken.None);
        Task<AsyncOperationLease> queued = store.EnterAsync("first", CancellationToken.None);
        Task closing = store.DisposeAsync().AsTask();
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => store.EnterAsync("first", CancellationToken.None));
        bool waitedForOwner = !closing.IsCompleted;
        held.Dispose();
        held.Dispose();
        using AsyncOperationLease next = await queued.WaitAsync(completionTimeout);
        bool waitedForQueue = !closing.IsCompleted;
        next.Dispose();
        next.Dispose();
        await closing.WaitAsync(completionTimeout);
        Assert.IsTrue(waitedForOwner);
        Assert.IsTrue(waitedForQueue);
        Assert.IsTrue(closing.IsCompletedSuccessfully);
    }

    private static PendingLogin CreatePending(TimeProvider clock, string hash)
    {
        return new PendingLogin
        {
            UserId = 1,
            TokenHash = hash,
            CodeId = 1,
            IssueUntil = clock.GetUtcNow().UtcDateTime
                .Add(LoginVerificationOptions.IssuanceLifetime)
        };
    }
}
