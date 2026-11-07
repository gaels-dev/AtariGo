using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using AtariGo.Server.Application.Friends;
using AtariGo.Server.Application.Sessions;
using AtariGo.Server.Data;
using AtariGo.Server.Tests.Rooms;

namespace AtariGo.Server.Tests.Friends;

[TestClass]
[TestCategory("Integration")]
public sealed class FriendServiceTests
{
    [TestMethod]
    public async Task ListFriendRequestsBySender_MultipleSenders_PagesOnlyOwnedPendingRequests()
    {
        // Arrange
        await using SocialTestDatabase database = await SocialTestDatabase.CreateAsync();
        await using AtariGoDbContext context = database.CreateContext();
        await SeedRequestsAsync(context);
        using var mutations = new FriendMutationCoordinator();
        FriendService service = CreateService(context, mutations);

        // Act
        SentFriendRequestsPage? first = await service.ListFriendRequestsBySenderAsync(
            "host", new SentFriendRequestsQuery(1, 0), CancellationToken.None);
        SentFriendRequestsPage? second = await service.ListFriendRequestsBySenderAsync(
            "host", new SentFriendRequestsQuery(1, first!.NextRequestId!.Value),
            CancellationToken.None);

        // Assert
        Assert.HasCount(1, first.Requests);
        Assert.AreEqual(1L, first.Requests[0].RequestId);
        Assert.AreEqual("GuestPlayer", first.Requests[0].RecipientUserName);
        Assert.HasCount(1, second!.Requests);
        Assert.AreEqual(3L, second.Requests[0].RequestId);
        Assert.IsNull(second.NextRequestId);
    }

    [TestMethod]
    public async Task CancelFriendRequest_OtherSenderAndRetry_ProtectsOwnershipAndIsIdempotent()
    {
        // Arrange
        await using SocialTestDatabase database = await SocialTestDatabase.CreateAsync();
        await using AtariGoDbContext context = database.CreateContext();
        await SeedRequestsAsync(context);
        using var mutations = new FriendMutationCoordinator();
        FriendService service = CreateService(context, mutations);

        // Act
        CancelFriendRequestResult? unauthorized = await service.CancelFriendRequestAsync(
            "other", 1, CancellationToken.None);
        bool stillPending = await context.FriendRequests.AnyAsync(
            request => request.FriendRequestId == 1);
        CancelFriendRequestResult? cancelled = await service.CancelFriendRequestAsync(
            "host", 1, CancellationToken.None);
        CancelFriendRequestResult? repeated = await service.CancelFriendRequestAsync(
            "host", 1, CancellationToken.None);

        // Assert
        Assert.AreEqual(CancelFriendRequestResult.NoLongerPending, unauthorized);
        Assert.IsTrue(stillPending);
        Assert.AreEqual(CancelFriendRequestResult.Cancelled, cancelled);
        Assert.AreEqual(CancelFriendRequestResult.NoLongerPending, repeated);
        Assert.IsFalse(await context.FriendRequests.AnyAsync(
            request => request.FriendRequestId == 1));
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task CancelAndAccept_CompetingRequests_OnlyOneTransitionWins(bool cancelFirst)
    {
        // Arrange
        await using SocialTestDatabase database = await SocialTestDatabase.CreateAsync();
        await using AtariGoDbContext senderContext = database.CreateContext();
        await using AtariGoDbContext recipientContext = database.CreateContext();
        await SeedRequestsAsync(senderContext);
        using var mutations = new FriendMutationCoordinator();
        using FriendMutationLease held = await mutations.EnterAsync(CancellationToken.None);
        FriendService sender = CreateService(senderContext, mutations);
        FriendService recipient = CreateService(recipientContext, mutations);
        var command = new RespondToFriendRequestCommand("guest", 1, true);

        // Act
        Task<CancelFriendRequestResult?> cancel;
        Task<FriendRequestResponseResult?> accept;
        if (cancelFirst)
        {
            cancel = sender.CancelFriendRequestAsync("host", 1, CancellationToken.None);
            accept = recipient.RespondToFriendRequestAsync(command, CancellationToken.None);
        }
        else
        {
            accept = recipient.RespondToFriendRequestAsync(command, CancellationToken.None);
            cancel = sender.CancelFriendRequestAsync("host", 1, CancellationToken.None);
        }

        held.Dispose();
        await Task.WhenAll(cancel, accept);

        // Assert
        Assert.AreEqual(cancelFirst ? CancelFriendRequestResult.Cancelled
            : CancelFriendRequestResult.NoLongerPending, await cancel);
        Assert.AreEqual(cancelFirst ? FriendRequestResponseResult.NotFound
            : FriendRequestResponseResult.Accepted, await accept);
        Assert.AreEqual(!cancelFirst, await senderContext.Friendships.AnyAsync());
    }

    [TestMethod]
    public async Task SendFriendRequest_CrossedConcurrentRequests_CreatesOnlyOnePendingRequest()
    {
        // Arrange
        await using SocialTestDatabase database = await SocialTestDatabase.CreateAsync();
        await using AtariGoDbContext firstContext = database.CreateContext();
        await using AtariGoDbContext secondContext = database.CreateContext();
        using var mutations = new FriendMutationCoordinator();
        using FriendMutationLease held = await mutations.EnterAsync(CancellationToken.None);
        FriendService first = CreateService(firstContext, mutations);
        FriendService second = CreateService(secondContext, mutations);

        // Act
        Task<FriendRequestResult?> send = first.SendFriendRequestAsync(
            "host", 2, CancellationToken.None);
        Task<FriendRequestResult?> crossed = second.SendFriendRequestAsync(
            "guest", 1, CancellationToken.None);
        held.Dispose();
        FriendRequestResult?[] results = await Task.WhenAll(send, crossed);

        // Assert
        CollectionAssert.AreEquivalent(new FriendRequestResult?[]
        {
            FriendRequestResult.Sent, FriendRequestResult.AlreadyPending
        }, results);
        Assert.AreEqual(1, await firstContext.FriendRequests.CountAsync());
    }

    [TestMethod]
    public async Task Requests_InvalidSession_CannotListOrCancel()
    {
        // Arrange
        await using SocialTestDatabase database = await SocialTestDatabase.CreateAsync();
        await using AtariGoDbContext context = database.CreateContext();
        await SeedRequestsAsync(context);
        using var mutations = new FriendMutationCoordinator();
        FriendService service = CreateService(context, mutations);

        // Act
        SentFriendRequestsPage? page = await service.ListFriendRequestsBySenderAsync(
            "invalid", new SentFriendRequestsQuery(20, 0), CancellationToken.None);
        CancelFriendRequestResult? cancelled = await service.CancelFriendRequestAsync(
            "invalid", 1, CancellationToken.None);

        // Assert
        Assert.IsNull(page);
        Assert.IsNull(cancelled);
        Assert.AreEqual(4, await context.FriendRequests.CountAsync());
    }

    [TestMethod]
    public async Task RespondToFriendRequest_OtherRecipient_CannotAcceptOrReject()
    {
        // Arrange
        await using SocialTestDatabase database = await SocialTestDatabase.CreateAsync();
        await using AtariGoDbContext context = database.CreateContext();
        await SeedRequestsAsync(context);
        using var mutations = new FriendMutationCoordinator();
        FriendService service = CreateService(context, mutations);

        // Act
        FriendRequestResponseResult? accepted = await service.RespondToFriendRequestAsync(
            new RespondToFriendRequestCommand("other", 1, true), CancellationToken.None);
        FriendRequestResponseResult? rejected = await service.RespondToFriendRequestAsync(
            new RespondToFriendRequestCommand("other", 1, false), CancellationToken.None);

        // Assert
        Assert.AreEqual(FriendRequestResponseResult.NotFound, accepted);
        Assert.AreEqual(FriendRequestResponseResult.NotFound, rejected);
        Assert.IsFalse(await context.Friendships.AnyAsync());
        Assert.AreEqual(4, await context.FriendRequests.CountAsync());
    }

    private static FriendService CreateService(
        AtariGoDbContext context,
        FriendMutationCoordinator mutations)
    {
        return new FriendService(context, new TestRoomSessionService(),
            new SessionConnectionRegistry(), TimeProvider.System, mutations);
    }

    private static Task<int> SeedRequestsAsync(AtariGoDbContext context)
    {
        return context.Database.ExecuteSqlRawAsync("""
            INSERT INTO FriendRequests VALUES
                (1, 1, 2, 'Pending', '2026-01-01', NULL),
                (2, 3, 2, 'Pending', '2026-01-01', NULL),
                (3, 1, 3, 'Pending', '2026-01-01', NULL),
                (4, 1, 2, 'Rejected', '2026-01-01', '2026-01-02');
            """);
    }
}
