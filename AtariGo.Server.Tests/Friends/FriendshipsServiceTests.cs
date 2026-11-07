using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using AtariGo.Contracts;
using AtariGo.Server.Application.Friends;
using AtariGo.Server.Application.Friendships;
using AtariGo.Server.Application.Sessions;
using AtariGo.Server.Data;
using AtariGo.Server.Tests.Rooms;

namespace AtariGo.Server.Tests.Friends;

[TestClass]
[TestCategory("Integration")]
public sealed class FriendshipsServiceTests
{
    [TestMethod]
    public async Task RemoveFriendship_ExistingPair_RemovesOnlyPairAndNotifiesBothUsers()
    {
        // Arrange
        await using SocialTestDatabase database = await SocialTestDatabase.CreateAsync();
        await using AtariGoDbContext context = database.CreateContext();
        await SeedFriendshipsAsync(context);
        using var mutations = new FriendMutationCoordinator();
        var registry = new SessionConnectionRegistry();
        using SessionConnectionLease host = registry.Register(new SessionConnectionInfo(
            1, 1, "Host", new[] { new FriendPresenceEntry(2, "GuestPlayer", false) }));
        using SessionConnectionLease guest = registry.Register(new SessionConnectionInfo(
            2, 2, "GuestPlayer", new[] { new FriendPresenceEntry(1, "Host", true) }));
        while (host.OutboundEvents.Reader.TryRead(out _)) { }
        FriendshipsService service = CreateService(context, registry, mutations);

        // Act
        RemoveFriendshipResult? result = await service.RemoveFriendshipAsync(
            "guest", 1, CancellationToken.None);
        RemoveFriendshipResult? repeated = await service.RemoveFriendshipAsync(
            "guest", 1, CancellationToken.None);

        // Assert
        Assert.AreEqual(RemoveFriendshipResult.Removed, result);
        Assert.AreEqual(RemoveFriendshipResult.AlreadyAbsent, repeated);
        Assert.AreEqual(1, await context.Friendships.CountAsync());
        Assert.IsTrue(await context.Friendships.AnyAsync(pair => pair.FriendUserId == 3));
        Assert.HasCount(0, host.ConnectionInfo.Friends);
        Assert.HasCount(0, guest.ConnectionInfo.Friends);
        Assert.IsTrue(host.OutboundEvents.Reader.TryRead(out SessionServerEvent? hostEvent));
        Assert.IsTrue(guest.OutboundEvents.Reader.TryRead(out SessionServerEvent? guestEvent));
        Assert.AreEqual(2, hostEvent.FriendshipRemoved.FriendUserId);
        Assert.AreEqual(1, guestEvent.FriendshipRemoved.FriendUserId);
    }

    [TestMethod]
    public async Task RemoveFriendship_InvalidSession_PreservesFriendships()
    {
        // Arrange
        await using SocialTestDatabase database = await SocialTestDatabase.CreateAsync();
        await using AtariGoDbContext context = database.CreateContext();
        await SeedFriendshipsAsync(context);
        using var mutations = new FriendMutationCoordinator();
        FriendshipsService service = CreateService(
            context, new SessionConnectionRegistry(), mutations);

        // Act
        RemoveFriendshipResult? result = await service.RemoveFriendshipAsync(
            "invalid", 2, CancellationToken.None);

        // Assert
        Assert.IsNull(result);
        Assert.AreEqual(2, await context.Friendships.CountAsync());
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    [DataRow(1)]
    public async Task RemoveFriendship_InvalidTarget_RejectsWithoutDeleting(int friendUserId)
    {
        // Arrange
        await using SocialTestDatabase database = await SocialTestDatabase.CreateAsync();
        await using AtariGoDbContext context = database.CreateContext();
        await SeedFriendshipsAsync(context);
        using var mutations = new FriendMutationCoordinator();
        FriendshipsService service = CreateService(
            context, new SessionConnectionRegistry(), mutations);

        // Act
        Task<RemoveFriendshipResult?> operation = service.RemoveFriendshipAsync(
            "host", friendUserId, CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<ArgumentException>(() => operation);
        Assert.AreEqual(2, await context.Friendships.CountAsync());
    }

    private static FriendshipsService CreateService(
        AtariGoDbContext context,
        SessionConnectionRegistry registry,
        FriendMutationCoordinator mutations)
    {
        return new FriendshipsService(context, new TestRoomSessionService(), registry,
            mutations, NullLogger<FriendshipsService>.Instance);
    }

    private static Task<int> SeedFriendshipsAsync(AtariGoDbContext context)
    {
        return context.Database.ExecuteSqlRawAsync("""
            INSERT INTO Friendships VALUES (1, 2, '2026-01-01'), (1, 3, '2026-01-01');
            """);
    }
}
