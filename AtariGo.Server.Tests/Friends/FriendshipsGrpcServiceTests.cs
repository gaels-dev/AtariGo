using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Grpc.Core;
using Grpc.Net.Client;

using AtariGo.Contracts;
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
