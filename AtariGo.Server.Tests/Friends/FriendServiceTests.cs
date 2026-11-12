using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using AtariGo.Server.Application.Friends;
using AtariGo.Server.Application.Sessions;
using AtariGo.Server.Application.Social;
using AtariGo.Server.Data;
using AtariGo.Server.Tests.Rooms;

namespace AtariGo.Server.Tests.Friends;

[TestClass]
[TestCategory("Integration")]
public sealed class FriendServiceTests
{
    [TestMethod]
    public async Task SearchUsers_ValidQuery_ExcludesRelatedAccountsAndOrdersMatches()
    {
        // Arrange
        await using SocialTestDatabase database = await SocialTestDatabase.CreateAsync();
        await using AtariGoDbContext context = database.CreateContext();
        await SeedSearchAccountsAsync(context);
        using var mutations = new FriendMutationCoordinator();
        FriendService service = CreateService(context, mutations);

        // Act
        IReadOnlyList<FriendCandidateEntry>? results = await service.SearchUsersAsync(
            "host", " Al ", CancellationToken.None);
        IReadOnlyList<FriendCandidateEntry>? requesterResults = await service.SearchUsersAsync(
            "host", "Host", CancellationToken.None);

        // Assert
        Assert.IsNotNull(results);
        CollectionAssert.AreEqual(new[] { "Alfred", "Alina" },
            results.Select(account => account.UserName).ToArray());
        Assert.IsNotNull(requesterResults);
        Assert.IsEmpty(requesterResults);
    }

    [TestMethod]
    public async Task SearchUsers_MoreThanMaximumMatches_ReturnsFirstTwentyInOrder()
    {
        // Arrange
        await using SocialTestDatabase database = await SocialTestDatabase.CreateAsync();
        await using AtariGoDbContext context = database.CreateContext();
        await SeedSearchResultLimitAsync(context);
        using var mutations = new FriendMutationCoordinator();
        FriendService service = CreateService(context, mutations);

        // Act
        IReadOnlyList<FriendCandidateEntry>? results = await service.SearchUsersAsync(
            "host", "Player", CancellationToken.None);

        // Assert
        Assert.IsNotNull(results);
        Assert.HasCount(20, results);
        Assert.AreEqual("Player00", results[0].UserName);
        Assert.AreEqual("Player19", results[^1].UserName);
    }

    [TestMethod]
    public async Task SearchUsers_InvalidSession_ReturnsNull()
    {
        // Arrange
        await using SocialTestDatabase database = await SocialTestDatabase.CreateAsync();
        await using AtariGoDbContext context = database.CreateContext();
        using var mutations = new FriendMutationCoordinator();
        FriendService service = CreateService(context, mutations);

        // Act
        IReadOnlyList<FriendCandidateEntry>? results = await service.SearchUsersAsync(
            "invalid", "Al", CancellationToken.None);

        // Assert
        Assert.IsNull(results);
    }

    [TestMethod]
    [DataRow("A")]
    [DataRow("ThisQueryIsTooLong")]
    [DataRow("  ")]
    public async Task SearchUsers_QueryOutsideAllowedLength_ThrowsArgumentException(
        string query)
    {
        // Arrange
        await using SocialTestDatabase database = await SocialTestDatabase.CreateAsync();
        await using AtariGoDbContext context = database.CreateContext();
        using var mutations = new FriendMutationCoordinator();
        FriendService service = CreateService(context, mutations);

        // Act and Assert
        await Assert.ThrowsExactlyAsync<ArgumentException>(() =>
            service.SearchUsersAsync("host", query, CancellationToken.None));
    }

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
        Task<SendFriendRequestResult?> send = first.SendFriendRequestAsync(
            "host", 2, CancellationToken.None);
        Task<SendFriendRequestResult?> crossed = second.SendFriendRequestAsync(
            "guest", 1, CancellationToken.None);
        held.Dispose();
        SendFriendRequestResult?[] results = await Task.WhenAll(send, crossed);

        // Assert
        CollectionAssert.AreEquivalent(new SendFriendRequestResult?[]
        {
            SendFriendRequestResult.Sent, SendFriendRequestResult.AlreadyPending
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

    [TestMethod]
    [DataRow(0L)]
    [DataRow(-1L)]
    public async Task RespondToFriendRequest_InvalidIdentifier_RejectsWithoutChanges(long requestId)
    {
        // Arrange
        await using SocialTestDatabase database = await SocialTestDatabase.CreateAsync();
        await using AtariGoDbContext context = database.CreateContext();
        await SeedRequestsAsync(context);
        using var mutations = new FriendMutationCoordinator();
        FriendService service = CreateService(context, mutations);
        var command = new RespondToFriendRequestCommand("guest", requestId, true);

        // Act
        ArgumentOutOfRangeException failure =
            await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() =>
                service.RespondToFriendRequestAsync(command, CancellationToken.None));

        // Assert
        Assert.AreEqual(requestId, failure.ActualValue);
        Assert.AreEqual(4, await context.FriendRequests.CountAsync());
        Assert.IsFalse(await context.Friendships.AnyAsync());
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task RespondToFriendRequest_SaveFails_RollsBackAcceptance(
        bool failFriendshipInsert)
    {
        // Arrange
        await using SocialTestDatabase database = await SocialTestDatabase.CreateAsync();
        await using AtariGoDbContext context = database.CreateContext();
        await SeedRequestsAsync(context);
        await InstallAcceptanceFailureAsync(context, failFriendshipInsert);
        using var mutations = new FriendMutationCoordinator();
        FriendService service = CreateService(context, mutations);
        var command = new RespondToFriendRequestCommand("guest", 1, true);

        // Act
        DbUpdateException failure = await Assert.ThrowsExactlyAsync<DbUpdateException>(() =>
            service.RespondToFriendRequestAsync(command, CancellationToken.None));

        // Assert
        Assert.IsNotNull(failure.InnerException);
        StringAssert.Contains(failure.InnerException.Message, "rejected by the test.");
        await using AtariGoDbContext verificationContext = database.CreateContext();
        Assert.AreEqual(4, await verificationContext.FriendRequests.CountAsync());
        Assert.IsTrue(await verificationContext.FriendRequests.AnyAsync(
            request => request.FriendRequestId == 1 && request.State == "Pending"));
        Assert.IsFalse(await verificationContext.Friendships.AnyAsync());
    }

    [TestMethod]
    [DataRow(true, false, FriendRequestResponseResult.Accepted)]
    [DataRow(false, false, FriendRequestResponseResult.Rejected)]
    [DataRow(true, true, FriendRequestResponseResult.AlreadyFriends)]
    public async Task RespondToFriendRequest_ValidDecision_CompletesRequest(
        bool accept,
        bool alreadyFriends,
        FriendRequestResponseResult expectedResult)
    {
        // Arrange
        await using SocialTestDatabase database = await SocialTestDatabase.CreateAsync();
        await using AtariGoDbContext context = database.CreateContext();
        await SeedRequestsAsync(context);
        if (alreadyFriends)
        {
            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO Friendships VALUES (1, 2, '2026-01-01');");
        }

        using var mutations = new FriendMutationCoordinator();
        FriendService service = CreateService(context, mutations);
        var command = new RespondToFriendRequestCommand("guest", 1, accept);

        // Act
        FriendRequestResponseResult? result = await service.RespondToFriendRequestAsync(
            command, CancellationToken.None);

        // Assert
        Assert.AreEqual(expectedResult, result);
        Assert.IsFalse(await context.FriendRequests.AnyAsync(
            request => request.FriendRequestId == 1));
        Assert.AreEqual(3, await context.FriendRequests.CountAsync());
        Assert.AreEqual(accept, await context.Friendships.AnyAsync());
    }

    [TestMethod]
    [DataRow(1, SendFriendRequestResult.CannotRequestSelf)]
    [DataRow(999, SendFriendRequestResult.UserNotFound)]
    [DataRow(2, SendFriendRequestResult.AlreadyFriends)]
    [DataRow(3, SendFriendRequestResult.AlreadyPending)]
    public async Task SendFriendRequest_Rejected_PreservesExistingData(
        int recipientUserId, SendFriendRequestResult expectedResult)
    {
        // Arrange
        await using SocialTestDatabase database = await SocialTestDatabase.CreateAsync();
        await using AtariGoDbContext context = database.CreateContext();
        await SeedRequestsAsync(context);
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO Friendships VALUES (1, 2, '2026-01-01');");
        using var mutations = new FriendMutationCoordinator();
        FriendService service = CreateService(context, mutations);

        // Act
        SendFriendRequestResult? result = await service.SendFriendRequestAsync(
            "host", recipientUserId, CancellationToken.None);

        // Assert
        Assert.AreEqual(expectedResult, result);
        Assert.AreEqual(4, await context.FriendRequests.CountAsync());
        Assert.AreEqual(1, await context.Friendships.CountAsync());
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

    private static Task<int> InstallAcceptanceFailureAsync(
        AtariGoDbContext context,
        bool failFriendshipInsert)
    {
        string triggerSql = failFriendshipInsert ? """
            CREATE TRIGGER RejectFriendshipInsert BEFORE INSERT ON Friendships
            BEGIN
                SELECT RAISE(ABORT, 'Friendship insert rejected by the test.');
            END;
            """ : """
            CREATE TRIGGER RejectRequestDeletion BEFORE DELETE ON FriendRequests
            BEGIN
                SELECT RAISE(ABORT, 'Request deletion rejected by the test.');
            END;
            """;
        return context.Database.ExecuteSqlRawAsync(triggerSql);
    }

    private static Task<int> SeedSearchAccountsAsync(AtariGoDbContext context)
    {
        return context.Database.ExecuteSqlRawAsync("""
            INSERT INTO Users VALUES
                (4, 'Alpine', 'alpine@example.test', 'Active', '2026-01-01', NULL, 1),
                (5, 'Alpha', 'alpha@example.test', 'Active', '2026-01-01', NULL, 1),
                (6, 'Ally', 'ally@example.test', 'Active', '2026-01-01', NULL, 1),
                (7, 'Alina', 'alina@example.test', 'Active', '2026-01-01', NULL, 1),
                (8, 'Alfred', 'alfred@example.test', 'Active', '2026-01-01', NULL, 1),
                (9, 'Alberta', 'alberta@example.test', 'Inactive', '2026-01-01', NULL, 1);
            INSERT INTO Friendships VALUES (4, 1, '2026-01-01');
            INSERT INTO FriendRequests VALUES
                (1, 1, 5, 'Pending', '2026-01-01', NULL),
                (2, 6, 1, 'Pending', '2026-01-01', NULL),
                (3, 1, 8, 'Rejected', '2026-01-01', '2026-01-02');
            """);
    }

    private static async Task SeedSearchResultLimitAsync(AtariGoDbContext context)
    {
        for (int userNumber = 0; userNumber < 25; userNumber++)
        {
            string userName = $"Player{userNumber:D2}";
            int userId = userNumber + 4;
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO Users VALUES
                    ({userId}, {userName}, {userName + "@example.test"}, 'Active',
                     '2026-01-01', NULL, 1);
                """);
        }
    }
}
