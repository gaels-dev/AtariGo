using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Grpc.Core;
using Grpc.Net.Client;

using AtariGo.Contracts;
using AtariGo.Server.Data;
using AtariGo.Server.Tests.Friends;

namespace AtariGo.Server.Tests.RoomInvitations;

[TestClass]
[TestCategory("Integration")]
public sealed class RoomInvitationsGrpcServiceTests
{
    [TestMethod]
    public async Task InvitationFlow_TwoClients_NotifiesAndAcceptsLatestConfiguration()
    {
        // Arrange
        await using SocialGrpcTestHost server = await SocialGrpcTestHost.StartAsync();
        await using AtariGoDbContext database = server.Database.CreateContext();
        await AddFriendshipAsync(database);
        using GrpcChannel channel = GrpcChannel.ForAddress(server.Address);
        var rooms = new Contracts.Rooms.RoomsClient(channel);
        var invitations = new Contracts.RoomInvitations.RoomInvitationsClient(channel);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        CallOptions host = Options("host", timeout.Token);
        CallOptions guest = Options("guest", timeout.Token);
        using AsyncDuplexStreamingCall<SessionClientEvent, SessionServerEvent> session =
            new Session.SessionClient(channel).Connect(guest);
        await session.RequestStream.WriteAsync(new SessionClientEvent
        {
            ClientConnected = new ClientConnected()
        });
        Assert.IsTrue(await session.ResponseStream.MoveNext(timeout.Token));
        CreatePrivateRoomResponse created = await rooms.CreatePrivateRoomAsync(CreateRoom(), host);

        // Act
        SendRoomInvitationResponse sent = await invitations.SendRoomInvitationAsync(
            new SendRoomInvitationRequest
            {
                RoomId = created.Room.RoomId, RecipientUserId = 2
            }, host);
        bool received = await session.ResponseStream.MoveNext(timeout.Token);
        UpdateRoomConfigurationResponse updated = await rooms.UpdateRoomConfigurationAsync(
            new UpdateRoomConfigurationRequest
            {
                RoomId = created.Room.RoomId, ExpectedVersion = created.Room.Version,
                Configuration = new RoomConfiguration
                {
                    CaptureGoal = 3, GameTimeSeconds = 600, TurnTimeSeconds = 30
                }
            }, host);
        RespondRoomInvitationResponse accepted = await invitations.RespondRoomInvitationAsync(
            new RespondRoomInvitationRequest
            {
                InvitationId = sent.Invitation.InvitationId, Accept = true
            }, guest);
        using AsyncServerStreamingCall<RoomSnapshot> watch = rooms.WatchRoom(
            new WatchRoomRequest { RoomId = created.Room.RoomId }, host);
        bool hostReceived = await watch.ResponseStream.MoveNext(timeout.Token);

        // Assert
        Assert.IsTrue(received && hostReceived);
        Assert.IsNotNull(session.ResponseStream.Current.RoomInvitationsChanged);
        Assert.AreEqual(RoomInvitationState.Accepted, accepted.Invitation.State);
        Assert.AreEqual(accepted.Room, watch.ResponseStream.Current);
        Assert.AreEqual(3, accepted.Room.Configuration.CaptureGoal);
        Assert.AreEqual(updated.Room.Version + 1, accepted.Room.Version);
        Assert.HasCount(2, accepted.Room.Participants);
    }

    [TestMethod]
    public async Task RespondRoomInvitation_FriendshipRemoved_RejectsAndPreservesRoom()
    {
        // Arrange
        await using SocialGrpcTestHost server = await SocialGrpcTestHost.StartAsync();
        await using AtariGoDbContext database = server.Database.CreateContext();
        await AddFriendshipAsync(database);
        using GrpcChannel channel = GrpcChannel.ForAddress(server.Address);
        var invitations = new Contracts.RoomInvitations.RoomInvitationsClient(channel);
        var rooms = new Contracts.Rooms.RoomsClient(channel);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        CallOptions host = Options("host", timeout.Token);
        CallOptions guest = Options("guest", timeout.Token);
        using AsyncDuplexStreamingCall<SessionClientEvent, SessionServerEvent> session =
            new Session.SessionClient(channel).Connect(guest);
        await session.RequestStream.WriteAsync(new SessionClientEvent
        {
            ClientConnected = new ClientConnected()
        });
        await session.ResponseStream.MoveNext(timeout.Token);
        CreatePrivateRoomResponse created = await rooms.CreatePrivateRoomAsync(CreateRoom(), host);
        SendRoomInvitationResponse sent = await invitations.SendRoomInvitationAsync(
            new SendRoomInvitationRequest
            {
                RoomId = created.Room.RoomId, RecipientUserId = 2
            }, host);
        await database.Database.ExecuteSqlRawAsync("DELETE FROM Friendships;");

        // Act
        RpcException failure = await Assert.ThrowsExactlyAsync<RpcException>(async () =>
            await invitations.RespondRoomInvitationAsync(new RespondRoomInvitationRequest
            {
                InvitationId = sent.Invitation.InvitationId, Accept = true
            }, guest));
        using AsyncServerStreamingCall<RoomSnapshot> watch = rooms.WatchRoom(
            new WatchRoomRequest { RoomId = created.Room.RoomId }, host);
        await watch.ResponseStream.MoveNext(timeout.Token);

        // Assert
        Assert.AreEqual(StatusCode.PermissionDenied, failure.StatusCode);
        Assert.AreEqual("NotFriends", failure.Status.Detail);
        Assert.HasCount(1, watch.ResponseStream.Current.Participants);
    }

    [TestMethod]
    [DataRow("unknown", StatusCode.Unauthenticated)]
    [DataRow("other", StatusCode.PermissionDenied)]
    public async Task SendRoomInvitation_InvalidIdentityOrFriendship_Rejects(
        string token, StatusCode expected)
    {
        // Arrange
        await using SocialGrpcTestHost server = await SocialGrpcTestHost.StartAsync();
        using GrpcChannel channel = GrpcChannel.ForAddress(server.Address);
        var invitations = new Contracts.RoomInvitations.RoomInvitationsClient(channel);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Act
        RpcException failure = await Assert.ThrowsExactlyAsync<RpcException>(async () =>
            await invitations.SendRoomInvitationAsync(new SendRoomInvitationRequest
            {
                RoomId = 1, RecipientUserId = 2
            }, Options(token, timeout.Token)));

        // Assert
        Assert.AreEqual(expected, failure.StatusCode);
    }

    [TestMethod]
    public async Task CancelRoomInvitation_Host_CanQueryCancellationAfterReconnect()
    {
        // Arrange
        await using SocialGrpcTestHost server = await SocialGrpcTestHost.StartAsync();
        await using AtariGoDbContext database = server.Database.CreateContext();
        await AddFriendshipAsync(database);
        using GrpcChannel channel = GrpcChannel.ForAddress(server.Address);
        var invitations = new Contracts.RoomInvitations.RoomInvitationsClient(channel);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        CallOptions host = Options("host", timeout.Token);
        CallOptions guest = Options("guest", timeout.Token);
        var sessions = new Session.SessionClient(channel);
        using AsyncDuplexStreamingCall<SessionClientEvent, SessionServerEvent> session =
            sessions.Connect(guest);
        await session.RequestStream.WriteAsync(new SessionClientEvent
        {
            ClientConnected = new ClientConnected()
        });
        await session.ResponseStream.MoveNext(timeout.Token);
        CreatePrivateRoomResponse room = await new Contracts.Rooms.RoomsClient(channel)
            .CreatePrivateRoomAsync(CreateRoom(), host);
        SendRoomInvitationResponse sent = await invitations.SendRoomInvitationAsync(
            new SendRoomInvitationRequest { RoomId = room.Room.RoomId, RecipientUserId = 2 }, host);

        // Act
        CancelRoomInvitationResponse cancelled = await invitations.CancelRoomInvitationAsync(
            new CancelRoomInvitationRequest { InvitationId = sent.Invitation.InvitationId }, host);
        using AsyncDuplexStreamingCall<SessionClientEvent, SessionServerEvent> reconnect =
            sessions.Connect(guest);
        await reconnect.RequestStream.WriteAsync(new SessionClientEvent
        {
            ClientConnected = new ClientConnected()
        });
        await reconnect.ResponseStream.MoveNext(timeout.Token);
        ListRoomInvitationsResponse received = await invitations.ListRoomInvitationsAsync(
            new ListRoomInvitationsRequest(), guest);
        ListRoomInvitationsResponse hidden = await invitations.ListRoomInvitationsAsync(
            new ListRoomInvitationsRequest(), Options("other", timeout.Token));

        // Assert
        Assert.AreEqual(RoomInvitationState.Cancelled, cancelled.Invitation.State);
        Assert.AreEqual(cancelled.Invitation, received.Invitations.Single());
        Assert.HasCount(0, hidden.Invitations);
    }

    private static Task AddFriendshipAsync(AtariGoDbContext database)
    {
        return database.Database.ExecuteSqlRawAsync(
            "INSERT INTO Friendships VALUES (1, 2, '2026-01-01');");
    }

    private static CreatePrivateRoomRequest CreateRoom()
    {
        return new CreatePrivateRoomRequest
        {
            Configuration = new RoomConfiguration
            {
                CaptureGoal = 1, GameTimeSeconds = 600, TurnTimeSeconds = 30
            }
        };
    }

    private static CallOptions Options(string token, CancellationToken cancellationToken)
    {
        return new CallOptions(new Metadata { { "authorization", $"Bearer {token}" } },
            deadline: DateTime.UtcNow.AddSeconds(10), cancellationToken: cancellationToken);
    }
}
