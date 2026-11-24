using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using AtariGo.Contracts;
using AtariGo.Diagnostics;
using AtariGo.Server.Application.Matchmaking;
using AtariGo.Server.Application.RoomInvitations;
using AtariGo.Server.Application.Rooms;
using AtariGo.Server.Application.Sessions;
using AtariGo.Server.Tests.Matchmaking;
using AtariGo.Server.Tests.RoomInvitations;

namespace AtariGo.Server.Tests.Diagnostics;

[TestClass]
[TestCategory("Integration")]
public sealed class LobbyLoggingTests
{
    private static readonly string[] roomOperations =
        ["RoomCreated", "RoomConfigured", "RoomJoined", "RoomLeft", "RoomClosed"];
    private static readonly string[] invitationOperations =
        ["InvitationCreated", "InvitationChanged"];

    private readonly TestContext _testContext;

    public LobbyLoggingTests(TestContext testContext)
    {
        _testContext = testContext;
    }

    [TestMethod]
    public async Task Rooms_RepeatedCommands_RecordOnlyActualTransitionsAsync()
    {
        // Arrange
        await using var host = new ServerLogTestHost(fileLevel: "Information");
        var connections = new SessionConnectionRegistry();
        var rooms = new RoomsService(TimeProvider.System, connections,
            host.Application.Services.GetRequiredService<ILogger<RoomsService>>());
        var owner = new LobbyParticipant(1, "private-owner");
        var guest = new LobbyParticipant(2, "private-guest");
        var configuration = new LobbyConfiguration(1, 600, 30);
        PrivateRoom room = rooms.CreatePrivateRoom(owner, configuration).Room!;
        var locator = new RoomLocator(0, room.InvitationCode);
        // Act
        rooms.CreatePrivateRoom(owner, configuration);
        RoomOperationResult configured = rooms.UpdateRoomConfiguration(
            new UpdateRoomConfigurationCommand(room.RoomId,
                new LobbyConfiguration(3, 600, 30), room.Version), owner.UserId);
        rooms.UpdateRoomConfiguration(new UpdateRoomConfigurationCommand(room.RoomId,
            configured.Room!.Configuration, configured.Room.Version), owner.UserId);
        rooms.JoinRoom(guest, locator);
        rooms.JoinRoom(guest, locator);
        rooms.LeaveRoom(guest.UserId, room.RoomId);
        rooms.LeaveRoom(guest.UserId, room.RoomId);
        rooms.LeaveRoom(owner.UserId, room.RoomId);
        List<JsonElement> entries = await host.ReadEventsAsync(_testContext.CancellationToken);
        // Assert
        Assert.AreSequenceEqual(roomOperations, entries.Select(Operation));
        Assert.IsTrue(entries.All(entry => entry.GetProperty("RoomId").GetInt32() == room.RoomId));
        Assert.DoesNotContain(message =>
            message.Contains("private-", StringComparison.Ordinal), host.Records.Messages);
        Assert.DoesNotContain(message =>
            message.Contains(room.InvitationCode, StringComparison.Ordinal), host.Records.Messages);
        Assert.IsEmpty(host.Records.Exceptions);
    }

    [TestMethod]
    [DataRow("accept", "Accepted")]
    [DataRow("reject", "Rejected")]
    [DataRow("cancel", "Cancelled")]
    [DataRow("close", "Unavailable")]
    [DataRow("expire", "Expired")]
    public async Task Invitations_RetriesAndRetention_DoNotDuplicateTransitionsAsync(
        string action, string expectedState)
    {
        // Arrange
        await using var host = new ServerLogTestHost(fileLevel: "Information");
        var connections = new SessionConnectionRegistry();
        using SessionConnectionLease recipient = connections.Register(
            new SessionConnectionInfo(2, 2, "private-recipient")
            {
                Friends = Array.Empty<FriendPresenceEntry>()
            });
        var time = new ManualTimeProvider();
        var rooms = new RoomsService(time, connections,
            host.Application.Services.GetRequiredService<ILogger<RoomsService>>());
        PrivateRoom room = rooms.CreatePrivateRoom(new LobbyParticipant(1, "private-owner"),
            new LobbyConfiguration(1, 600, 30)).Room!;
        var command = new SendRoomInvitationCommand(room.RoomId, 2);
        var participant = new LobbyParticipant(2, "private-recipient");
        RoomInvitationEntry invitation = rooms.SendRoomInvitation(command, participant, 1)
            .Invitation!;
        // Act
        rooms.SendRoomInvitation(command, participant, 1);
        ApplyInvitationAction(rooms, invitation, action);
        ApplyInvitationAction(rooms, invitation, action);
        time.Advance(TimeSpan.FromMinutes(2));
        rooms.ListRoomInvitations(2, null);
        time.Advance(TimeSpan.FromMinutes(2));
        rooms.ListRoomInvitations(2, null);
        List<JsonElement> entries = await host.ReadEventsAsync(_testContext.CancellationToken);
        JsonElement[] invitations = entries.Where(entry =>
            Operation(entry).StartsWith("Invitation", StringComparison.Ordinal)).ToArray();
        // Assert
        Assert.AreSequenceEqual(invitationOperations, invitations.Select(Operation));
        Assert.AreEqual(expectedState, invitations[1].GetProperty("Status")
            .GetProperty("Value").GetString());
        Assert.IsTrue(invitations.All(entry => entry.GetProperty("InvitationId").GetInt64()
            == invitation.InvitationId));
        Assert.DoesNotContain(message =>
            message.Contains("private-", StringComparison.Ordinal), host.Records.Messages);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task Matchmaking_CancellationAndCompletion_RecordOncePerMatchAsync(bool succeeds)
    {
        // Arrange
        await using var host = new ServerLogTestHost(fileLevel: "Information");
        var time = new ManualTimeProvider();
        var lifecycle = new ControlledPublicGameLifecycleService();
        var queue = new InMemoryPublicMatchmakingQueue(time,
            host.Application.Services
                .GetRequiredService<ILogger<InMemoryPublicMatchmakingQueue>>());
        var service = new PublicMatchmakingService(queue, lifecycle, time);
        var firstPlayer = new MatchmakingPlayer
        {
            PlayerKey = "private-credential",
            DisplayName = "private-display",
            UserId = 1
        };
        MatchmakingTicket first = queue.Join(firstPlayer)!;
        // Act
        queue.Join(firstPlayer);
        MatchmakingTicket second = queue.Join(new MatchmakingPlayer
        {
            PlayerKey = "private-other",
            DisplayName = "private-other"
        })!;
        queue.Disconnect(second);
        queue.Disconnect(second);
        queue.Disconnect(first);
        first = queue.Join(firstPlayer)!;
        queue.Join(new MatchmakingPlayer { PlayerKey = "private-third", DisplayName = "private" });
        await using IAsyncEnumerator<MatchmakingEvent> reader = service.ReadEventsAsync(
            first, _testContext.CancellationToken)
            .GetAsyncEnumerator(_testContext.CancellationToken);
        Assert.IsTrue(await reader.MoveNextAsync());
        Assert.IsTrue(await reader.MoveNextAsync());
        time.Advance(TimeSpan.FromSeconds(PublicGameRules.CountdownSeconds));
        Task<bool> reading = reader.MoveNextAsync().AsTask();
        await lifecycle.Started.Task.WaitAsync(_testContext.CancellationToken);
        Assert.DoesNotContain(entry => entry.Definition.Operation == "MatchStarted",
            host.Records.States.OfType<DiagnosticEvent>());
        if (succeeds)
        {
            lifecycle.Completion.SetResult(42);
            Assert.IsTrue(await reading);
        }
        else
        {
            lifecycle.Completion.SetException(new InvalidOperationException("private-failure"));
            await Assert.ThrowsAsync<InvalidOperationException>(() => reading);
        }
        queue.Disconnect(first);
        List<JsonElement> entries = await host.ReadEventsAsync(_testContext.CancellationToken);
        // Assert
        Assert.HasCount(2, entries.Where(entry => Operation(entry) == "MatchPaired"));
        JsonElement cancellation = entries.Single(entry =>
            Operation(entry) == "CountdownCancelled");
        Guid cancelledId = cancellation.GetProperty("OperationId").GetGuid();
        Assert.AreNotEqual(Guid.Empty, cancelledId);
        JsonElement completion = entries.Single(entry =>
            Operation(entry) == (succeeds ? "MatchStarted" : "MatchStartFailed"));
        Guid completedId = completion.GetProperty("OperationId").GetGuid();
        Assert.AreNotEqual(cancelledId, completedId);
        Assert.AreEqual(1, lifecycle.CreationCount);
        Assert.AreEqual(succeeds ? "Succeeded" : "Unconfirmed",
            completion.GetProperty("Outcome").GetString());
        Assert.HasCount(1, entries.Where(entry => Operation(entry) == "SearchRequest"));
        Assert.DoesNotContain(message =>
            message.Contains("private", StringComparison.Ordinal), host.Records.Messages);
    }

    private static string Operation(JsonElement entry)
    {
        return entry.GetProperty("Operation").GetString()!;
    }

    private static void ApplyInvitationAction(RoomsService rooms,
        RoomInvitationEntry invitation, string action)
    {
        switch (action)
        {
            case "accept":
            case "reject":
                rooms.RespondRoomInvitation(new RespondRoomInvitationCommand(
                    invitation.InvitationId, action == "accept"),
                    new LobbyParticipant(2, "private"));
                break;
            case "cancel":
                rooms.CancelRoomInvitation(invitation.InvitationId, 1);
                break;
            case "close":
                rooms.LeaveRoom(1, invitation.RoomId);
                break;
        }
    }
}
