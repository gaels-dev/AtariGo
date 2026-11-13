using Microsoft.Extensions.Logging.Abstractions;


using AtariGo.Server.Application.RoomInvitations;
using AtariGo.Server.Application.Rooms;
using AtariGo.Server.Application.Sessions;

namespace AtariGo.Server.Tests.RoomInvitations;

[TestClass]
[TestCategory("Unit")]
public sealed class RoomsServiceTests
{
    [TestMethod]
    public void SendRoomInvitation_Duplicate_ReturnsSamePendingInvitationAndNotifiesRecipient()
    {
        // Arrange
        var connections = new SessionConnectionRegistry();
        using SessionConnectionLease guest = Connect(connections, 2);
        var rooms = new RoomsService(
            TimeProvider.System, connections, NullLogger<RoomsService>.Instance);
        PrivateRoom room = CreateRoom(rooms);
        var command = new SendRoomInvitationCommand(room.RoomId, 2);

        // Act
        RoomInvitationResult first = rooms.SendRoomInvitation(
            command, new LobbyParticipant(2, "Guest"), 1);
        RoomInvitationResult duplicate = rooms.SendRoomInvitation(
            command, new LobbyParticipant(2, "Guest"), 1);
        bool notified = guest.OutboundEvents.Reader.TryRead(out var notification);

        // Assert
        Assert.AreEqual(RoomInvitationOperationStatus.Success, first.Status);
        Assert.AreEqual(first.Invitation, duplicate.Invitation);
        Assert.IsTrue(notified);
        Assert.IsNotNull(notification?.RoomInvitationsChanged);
        Assert.HasCount(1, rooms.ListRoomInvitations(2, null));
        Assert.HasCount(0, rooms.ListRoomInvitations(3, room.RoomId));
    }

    [TestMethod]
    public async Task RespondRoomInvitation_ConcurrentAcceptances_AssignsOnlyOneOpponent()
    {
        // Arrange
        var connections = new SessionConnectionRegistry();
        using SessionConnectionLease second = Connect(connections, 2);
        using SessionConnectionLease third = Connect(connections, 3);
        var rooms = new RoomsService(
            TimeProvider.System, connections, NullLogger<RoomsService>.Instance);
        PrivateRoom room = CreateRoom(rooms);
        RoomInvitationEntry first = Send(rooms, room.RoomId, 2);
        RoomInvitationEntry other = Send(rooms, room.RoomId, 3);

        // Act
        Task<RoomInvitationResult> left = Task.Run(() => rooms.RespondRoomInvitation(
            new RespondRoomInvitationCommand(first.InvitationId, true),
            new LobbyParticipant(2, "B")));
        Task<RoomInvitationResult> right = Task.Run(() => rooms.RespondRoomInvitation(
            new RespondRoomInvitationCommand(other.InvitationId, true),
            new LobbyParticipant(3, "C")));
        RoomInvitationResult[] results = await Task.WhenAll(left, right);
        RoomInvitationResult winner = results.Single(result =>
            result.Status == RoomInvitationOperationStatus.Success);

        // Assert
        Assert.AreEqual(1, results.Count(
            result => result.Status == RoomInvitationOperationStatus.Success));
        Assert.IsNotNull(winner.Room?.Opponent);
        Assert.AreEqual(RoomInvitationStatus.Accepted, winner.Invitation?.State);
        Assert.HasCount(1, rooms.ListRoomInvitations(1, room.RoomId).Where(invitation =>
            invitation.State == RoomInvitationStatus.Unavailable).ToArray());
    }

    [TestMethod]
    public void RespondRoomInvitation_AfterConfigurationChange_UsesLatestRoomAndIsRepeatable()
    {
        // Arrange
        var connections = new SessionConnectionRegistry();
        using SessionConnectionLease guest = Connect(connections, 2);
        var rooms = new RoomsService(
            TimeProvider.System, connections, NullLogger<RoomsService>.Instance);
        PrivateRoom room = CreateRoom(rooms);
        RoomInvitationEntry invitation = Send(rooms, room.RoomId, 2);
        rooms.UpdateRoomConfiguration(new UpdateRoomConfigurationCommand(
            room.RoomId, new LobbyConfiguration(5, 600, 30), room.Version), 1);
        var decision = new RespondRoomInvitationCommand(invitation.InvitationId, true);

        // Act
        RoomInvitationResult accepted = rooms.RespondRoomInvitation(
            decision, new LobbyParticipant(2, "Guest"));
        RoomInvitationResult repeated = rooms.RespondRoomInvitation(
            decision, new LobbyParticipant(2, "Guest"));

        // Assert
        Assert.AreEqual(RoomInvitationOperationStatus.Success, accepted.Status);
        Assert.AreEqual(5, accepted.Room?.Configuration.CaptureGoal);
        Assert.AreEqual(accepted.Room, repeated.Room);
    }

    [TestMethod]
    [DataRow("cancel")]
    [DataRow("reject")]
    [DataRow("close")]
    [DataRow("code")]
    [DataRow("expire")]
    public void RespondRoomInvitation_InvalidatedInvitation_CannotJoin(string scenario)
    {
        // Arrange
        var connections = new SessionConnectionRegistry();
        using SessionConnectionLease guest = Connect(connections, 2);
        var time = new ManualTimeProvider();
        var rooms = new RoomsService(time, connections, NullLogger<RoomsService>.Instance);
        PrivateRoom room = CreateRoom(rooms);
        RoomInvitationEntry invitation = Send(rooms, room.RoomId, 2);

        // Act
        Invalidate(rooms, invitation, scenario);
        if (scenario == "expire")
        {
            time.Advance(TimeSpan.FromMinutes(2));
        }

        RoomInvitationResult result = rooms.RespondRoomInvitation(
            new RespondRoomInvitationCommand(invitation.InvitationId, true),
            new LobbyParticipant(2, "Guest"));

        // Assert
        Assert.AreNotEqual(RoomInvitationOperationStatus.Success, result.Status);
    }

    [TestMethod]
    public void InvitationOperations_InvalidActorOrOfflineRecipient_RejectsWithoutMutation()
    {
        // Arrange
        var connections = new SessionConnectionRegistry();
        using SessionConnectionLease guest = Connect(connections, 2);
        var rooms = new RoomsService(
            TimeProvider.System, connections, NullLogger<RoomsService>.Instance);
        PrivateRoom room = CreateRoom(rooms);
        RoomInvitationEntry invitation = Send(rooms, room.RoomId, 2);

        // Act
        RoomInvitationResult nonHost = rooms.SendRoomInvitation(
            new SendRoomInvitationCommand(room.RoomId, 2), new LobbyParticipant(2, "B"), 3);
        RoomInvitationResult offline = rooms.SendRoomInvitation(
            new SendRoomInvitationCommand(room.RoomId, 3), new LobbyParticipant(3, "C"), 1);
        RoomInvitationResult stranger = rooms.RespondRoomInvitation(
            new RespondRoomInvitationCommand(invitation.InvitationId, true),
            new LobbyParticipant(3, "C"));
        RoomInvitationResult cancellation = rooms.CancelRoomInvitation(invitation.InvitationId, 2);

        // Assert
        Assert.AreEqual(RoomInvitationOperationStatus.PermissionDenied, nonHost.Status);
        Assert.AreEqual(RoomInvitationOperationStatus.RecipientOffline, offline.Status);
        Assert.AreEqual(RoomInvitationOperationStatus.PermissionDenied, stranger.Status);
        Assert.AreEqual(RoomInvitationOperationStatus.PermissionDenied, cancellation.Status);
        Assert.AreEqual(RoomInvitationStatus.Pending,
            rooms.ListRoomInvitations(2, null).Single().State);
    }

    [TestMethod]
    public void RemoveExpiredRoomsAndInvitations_ExpiredEntries_NotifiesAndEventuallyReleasesMemory()
    {
        // Arrange
        var connections = new SessionConnectionRegistry();
        using SessionConnectionLease guest = Connect(connections, 2);
        var time = new ManualTimeProvider();
        var rooms = new RoomsService(time, connections, NullLogger<RoomsService>.Instance);
        PrivateRoom room = CreateRoom(rooms);
        Send(rooms, room.RoomId, 2);
        guest.OutboundEvents.Reader.TryRead(out _);

        // Act
        time.Advance(TimeSpan.FromMinutes(2));
        rooms.RemoveExpiredRoomsAndInvitations();
        bool notified = guest.OutboundEvents.Reader.TryRead(out _);
        RoomInvitationStatus state = rooms.ListRoomInvitations(2, null).Single().State;
        time.Advance(TimeSpan.FromMinutes(2));
        rooms.RemoveExpiredRoomsAndInvitations();

        // Assert
        Assert.IsTrue(notified);
        Assert.AreEqual(RoomInvitationStatus.Expired, state);
        Assert.HasCount(0, rooms.ListRoomInvitations(2, null));
    }

    private static SessionConnectionLease Connect(SessionConnectionRegistry registry, int userId)
    {
        return registry.Register(new SessionConnectionInfo(userId, userId, "Player",
            Array.Empty<FriendPresenceEntry>()));
    }

    private static PrivateRoom CreateRoom(RoomsService rooms)
    {
        return rooms.CreatePrivateRoom(new LobbyParticipant(1, "Host"),
            new LobbyConfiguration(1, 600, 30)).Room!;
    }

    private static RoomInvitationEntry Send(RoomsService rooms, int roomId, int recipientId)
    {
        return rooms.SendRoomInvitation(new SendRoomInvitationCommand(roomId, recipientId),
            new LobbyParticipant(recipientId, "Recipient"), 1).Invitation!;
    }

    private static void Invalidate(
        RoomsService rooms, RoomInvitationEntry invitation, string scenario)
    {
        switch (scenario)
        {
            case "cancel":
                rooms.CancelRoomInvitation(invitation.InvitationId, 1);
                break;
            case "reject":
                rooms.RespondRoomInvitation(new RespondRoomInvitationCommand(
                    invitation.InvitationId, false), new LobbyParticipant(2, "B"));
                break;
            case "close":
                rooms.LeaveRoom(1, invitation.RoomId);
                break;
            case "code":
                rooms.JoinRoom(new LobbyParticipant(3, "C"),
                    new RoomLocator(0, invitation.RoomCode));
                break;
        }
    }
}
