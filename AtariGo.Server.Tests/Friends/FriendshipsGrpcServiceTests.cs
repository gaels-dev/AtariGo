using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Grpc.Core;
using Grpc.Net.Client;

using AtariGo.Contracts;
using AtariGo.Server.Application.Friendships;
using AtariGo.Server.Data;

namespace AtariGo.Server.Tests.Friends;

[TestClass]
[TestCategory("Integration")]
public sealed class FriendshipsGrpcServiceTests
{
    private const int TestTimeoutSeconds = 10;

    [TestMethod]
    public async Task RemoveFriendship_TwoConnectedClients_ReceiveRemovalAndReconnectWithoutFriend()
    {
        // Arrange
        await using SocialGrpcTestHost server = await SocialGrpcTestHost.StartAsync();
        await using AtariGoDbContext context = server.Database.CreateContext();
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO Friendships VALUES (1, 2, '2026-01-01');");
        using GrpcChannel channel = GrpcChannel.ForAddress(server.Address);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(TestTimeoutSeconds));
        var sessions = new Session.SessionClient(channel);
        var friendships = new Friendships.FriendshipsClient(channel);
        using AsyncDuplexStreamingCall<SessionClientEvent, SessionServerEvent> host =
            sessions.Connect(Options("host", timeout.Token));
        await host.RequestStream.WriteAsync(Connected());
        Assert.IsTrue(await host.ResponseStream.MoveNext(timeout.Token));
        using AsyncDuplexStreamingCall<SessionClientEvent, SessionServerEvent> guest =
            sessions.Connect(Options("guest", timeout.Token));
        await guest.RequestStream.WriteAsync(Connected());
        Assert.IsTrue(await guest.ResponseStream.MoveNext(timeout.Token));
        Assert.IsTrue(await host.ResponseStream.MoveNext(timeout.Token));

        // Act
        RemoveFriendshipResponse removed = await friendships.RemoveFriendshipAsync(
            new RemoveFriendshipRequest { FriendUserId = 2 }, Options("host", timeout.Token));
        bool hostReceived = await host.ResponseStream.MoveNext(timeout.Token);
        bool guestReceived = await guest.ResponseStream.MoveNext(timeout.Token);
        using AsyncDuplexStreamingCall<SessionClientEvent, SessionServerEvent> reconnected =
            sessions.Connect(Options("host", timeout.Token));
        await reconnected.RequestStream.WriteAsync(Connected());
        bool receivedSnapshot = await reconnected.ResponseStream.MoveNext(timeout.Token);

        // Assert
        Assert.AreEqual(RemoveFriendshipStatus.Removed, removed.Status);
        Assert.IsTrue(hostReceived);
        Assert.IsTrue(guestReceived);
        Assert.AreEqual(2, host.ResponseStream.Current.FriendshipRemoved.FriendUserId);
        Assert.AreEqual(1, guest.ResponseStream.Current.FriendshipRemoved.FriendUserId);
        Assert.IsFalse(await context.Friendships.AnyAsync());
        Assert.IsTrue(receivedSnapshot);
        Assert.HasCount(0, reconnected.ResponseStream.Current.FriendPresenceSnapshot.Friends);
    }

    [TestMethod]
    public async Task RemoveFriendship_InvalidSessionOrTarget_ReturnsControlledGrpcStatus()
    {
        // Arrange
        await using SocialGrpcTestHost server = await SocialGrpcTestHost.StartAsync();
        using GrpcChannel channel = GrpcChannel.ForAddress(server.Address);
        var client = new Friendships.FriendshipsClient(channel);

        // Act
        RpcException missingToken = await Assert.ThrowsAsync<RpcException>(() =>
            client.RemoveFriendshipAsync(new RemoveFriendshipRequest { FriendUserId = 2 })
                .ResponseAsync);
        RpcException invalidSession = await Assert.ThrowsAsync<RpcException>(() =>
            client.RemoveFriendshipAsync(new RemoveFriendshipRequest { FriendUserId = 2 },
                Options("invalid", CancellationToken.None)).ResponseAsync);
        RpcException self = await Assert.ThrowsAsync<RpcException>(() =>
            client.RemoveFriendshipAsync(new RemoveFriendshipRequest { FriendUserId = 1 },
                Options("host", CancellationToken.None)).ResponseAsync);

        // Assert
        Assert.AreEqual(StatusCode.Unauthenticated, missingToken.StatusCode);
        Assert.AreEqual(StatusCode.Unauthenticated, invalidSession.StatusCode);
        Assert.AreEqual(StatusCode.InvalidArgument, self.StatusCode);
    }

    [TestMethod]
    [DataRow("read", StatusCode.Internal)]
    [DataRow("transientRead", StatusCode.Unavailable)]
    [DataRow("save", StatusCode.Internal)]
    [DataRow("transientSave", StatusCode.Unavailable)]
    [DataRow("conflict", StatusCode.Aborted)]
    [DataRow("argument", StatusCode.Unknown)]
    [DataRow("range", StatusCode.Unknown)]
    [DataRow("targetArgument", StatusCode.InvalidArgument)]
    [DataRow("targetRange", StatusCode.InvalidArgument)]
    public async Task RemoveFriendship_Failure_ReturnsSafeStatusWithoutRepeating(
        string failureKind, StatusCode expectedStatus)
    {
        // Arrange
        var service = new FailingFriendshipsService(() => FailRemovalAsync(failureKind));
        await using SocialGrpcTestHost server = await SocialGrpcTestHost.StartAsync(
            friendshipsService: service);
        using GrpcChannel channel = GrpcChannel.ForAddress(server.Address);
        var client = new Friendships.FriendshipsClient(channel);

        // Act
        RpcException failure = await Assert.ThrowsExactlyAsync<RpcException>(() =>
            client.RemoveFriendshipAsync(new RemoveFriendshipRequest { FriendUserId = 2 },
                Options("host", CancellationToken.None)).ResponseAsync);

        // Assert
        Assert.AreEqual(expectedStatus, failure.StatusCode);
        Assert.IsFalse(failure.Status.Detail.Contains("Private", StringComparison.Ordinal));
        Assert.AreEqual(1, service.InvocationCount);
    }

    [TestMethod]
    [DataRow(StatusCode.Cancelled)]
    [DataRow(StatusCode.PermissionDenied)]
    public async Task RemoveFriendship_GrpcFailure_PreservesOriginalStatus(StatusCode status)
    {
        // Arrange
        var service = new FailingFriendshipsService(() =>
            throw new RpcException(new Status(status, "Controlled failure.")));
        await using SocialGrpcTestHost server = await SocialGrpcTestHost.StartAsync(
            friendshipsService: service);
        using GrpcChannel channel = GrpcChannel.ForAddress(server.Address);
        var client = new Friendships.FriendshipsClient(channel);

        // Act
        RpcException failure = await Assert.ThrowsExactlyAsync<RpcException>(() =>
            client.RemoveFriendshipAsync(new RemoveFriendshipRequest { FriendUserId = 2 },
                Options("host", CancellationToken.None)).ResponseAsync);

        // Assert
        Assert.AreEqual(status, failure.StatusCode);
        Assert.AreEqual("Controlled failure.", failure.Status.Detail);
        Assert.AreEqual(1, service.InvocationCount);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public async Task RemoveFriendship_NonpositiveIdentifier_ReturnsInvalidArgument(
        int friendUserId)
    {
        // Arrange
        await using SocialGrpcTestHost server = await SocialGrpcTestHost.StartAsync();
        using GrpcChannel channel = GrpcChannel.ForAddress(server.Address);
        var client = new Friendships.FriendshipsClient(channel);
        var removal = new RemoveFriendshipRequest { FriendUserId = friendUserId };

        // Act
        RpcException failure = await Assert.ThrowsExactlyAsync<RpcException>(() =>
            client.RemoveFriendshipAsync(removal,
                Options("host", CancellationToken.None)).ResponseAsync);

        // Assert
        Assert.AreEqual(StatusCode.InvalidArgument, failure.StatusCode);
    }

    [TestMethod]
    public async Task RemoveFriendship_DeleteFails_PreservesPairAndReleasesCoordinator()
    {
        // Arrange
        await using SocialGrpcTestHost server = await SocialGrpcTestHost.StartAsync();
        await using AtariGoDbContext context = server.Database.CreateContext();
        await context.Database.ExecuteSqlRawAsync("""
            INSERT INTO Friendships VALUES (1, 2, '2026-01-01');
            CREATE TRIGGER RejectFriendshipDeletion BEFORE DELETE ON Friendships
            BEGIN
                SELECT RAISE(ABORT, 'Private database detail.');
            END;
            """);
        using GrpcChannel channel = GrpcChannel.ForAddress(server.Address);
        var client = new Friendships.FriendshipsClient(channel);
        var removal = new RemoveFriendshipRequest { FriendUserId = 2 };

        // Act
        RpcException failure = await Assert.ThrowsExactlyAsync<RpcException>(() =>
            client.RemoveFriendshipAsync(removal, Options("host", CancellationToken.None))
                .ResponseAsync);

        // Assert
        Assert.AreEqual(StatusCode.Internal, failure.StatusCode);
        Assert.IsFalse(failure.Status.Detail.Contains("Private", StringComparison.Ordinal));
        Assert.AreEqual(1, await context.Friendships.CountAsync());
        await context.Database.ExecuteSqlRawAsync("DROP TRIGGER RejectFriendshipDeletion;");
        RemoveFriendshipResponse retried = await client.RemoveFriendshipAsync(
            removal, Options("host", CancellationToken.None));
        Assert.AreEqual(RemoveFriendshipStatus.Removed, retried.Status);
        Assert.IsFalse(await context.Friendships.AnyAsync());
    }

    private static Task<RemoveFriendshipResult?> FailRemovalAsync(string failureKind)
    {
        switch (failureKind)
        {
            case "read":
                throw new FriendTestDatabaseException(false);
            case "transientRead":
                throw new FriendTestDatabaseException(true);
            case "save":
                throw new DbUpdateException("Private persistence detail.");
            case "transientSave":
                throw new DbUpdateException("Private persistence detail.",
                    new FriendTestDatabaseException(true));
            case "conflict":
                throw new DbUpdateConcurrencyException("Private concurrency detail.");
            case "argument":
                throw new ArgumentException("Private dependency detail.", "configuration");
            case "range":
                throw new ArgumentOutOfRangeException(
                    "configuration", "Private dependency detail.");
            case "targetArgument":
                throw new ArgumentException("Private argument detail.", "friendUserId");
            case "targetRange":
                throw new ArgumentOutOfRangeException("friendUserId", "Private argument detail.");
            default:
                throw new ArgumentOutOfRangeException(nameof(failureKind));
        }
    }

    private static SessionClientEvent Connected()
    {
        return new SessionClientEvent { ClientConnected = new ClientConnected() };
    }

    private static CallOptions Options(string token, CancellationToken cancellationToken)
    {
        return new CallOptions(new Metadata { { "authorization", $"Bearer {token}" } },
            DateTime.UtcNow.AddSeconds(TestTimeoutSeconds), cancellationToken);
    }
}
