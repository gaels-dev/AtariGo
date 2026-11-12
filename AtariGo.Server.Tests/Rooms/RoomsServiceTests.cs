using Microsoft.Extensions.Logging.Abstractions;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using AtariGo.Server.Application.Rooms;
using AtariGo.Server.Application.Sessions;

namespace AtariGo.Server.Tests.Rooms;

[TestClass]
[TestCategory("Unit")]
public sealed class RoomsServiceTests
{
    private const long SavedGameId = 42;

    [TestMethod]
    public async Task ReserveStart_ConcurrentRequests_PublishesOnlyConfirmedGame()
    {
        // Arrange
        var service = new RoomsService(
            TimeProvider.System,
            new SessionConnectionRegistry(),
            NullLogger<RoomsService>.Instance);
        PrivateRoom room = CreateRoomWithOpponent(service);
        var request = new RoomStartRequest(room.RoomId, room.Version, room.Host.UserId);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using IAsyncEnumerator<RoomOperationResult> observer = service.WatchRoomAsync(
            new RoomObservation(room.Opponent!.UserId, room.RoomId, room.Version),
            timeout.Token).GetAsyncEnumerator();

        // Act
        Task<bool> nextUpdate = observer.MoveNextAsync().AsTask();
        RoomStartReservation reservation = service.ReserveStart(request).Reservation!;
        RoomStartReservationResult competingStart = service.ReserveStart(request);
        bool publishedBeforeSaving = nextUpdate.IsCompleted;
        RoomOperationResult confirmed = service.CompleteStart(reservation, SavedGameId);
        RoomOperationResult repeatedConfirmation = service.CompleteStart(
            reservation, SavedGameId);
        bool received = await nextUpdate;
        RoomStartReservationResult retried = service.ReserveStart(request);

        // Assert
        Assert.IsFalse(publishedBeforeSaving);
        Assert.AreEqual(RoomOperationStatus.StartInProgress, competingStart.Status);
        Assert.IsTrue(received);
        Assert.AreEqual(SavedGameId, confirmed.Room?.GameId);
        Assert.AreEqual(confirmed.Room, repeatedConfirmation.Room);
        Assert.AreEqual(RoomOperationStatus.Success, repeatedConfirmation.Status);
        Assert.AreEqual(confirmed.Room, observer.Current.Room);
        Assert.AreEqual(confirmed.Room, retried.Room);
        Assert.IsNull(retried.Reservation);
    }

    [TestMethod]
    public void ReserveStart_StaleVersionOrOpponent_RejectsStart()
    {
        // Arrange
        var service = new RoomsService(
            TimeProvider.System,
            new SessionConnectionRegistry(),
            NullLogger<RoomsService>.Instance);
        PrivateRoom room = CreateRoomWithOpponent(service);
        var stale = new RoomStartRequest(room.RoomId, room.Version - 1, room.Host.UserId);
        var opponent = new RoomStartRequest(room.RoomId, room.Version, room.Opponent!.UserId);

        // Act
        RoomStartReservationResult staleStart = service.ReserveStart(stale);
        RoomStartReservationResult opponentStart = service.ReserveStart(opponent);

        // Assert
        Assert.AreEqual(RoomOperationStatus.VersionMismatch, staleStart.Status);
        Assert.AreEqual(RoomOperationStatus.NotMember, opponentStart.Status);
    }

    [TestMethod]
    public void CancelStart_ReservationReleasedForRetry()
    {
        // Arrange
        var service = new RoomsService(
            TimeProvider.System,
            new SessionConnectionRegistry(),
            NullLogger<RoomsService>.Instance);
        PrivateRoom room = CreateRoomWithOpponent(service);
        var request = new RoomStartRequest(room.RoomId, room.Version, room.Host.UserId);

        // Act
        RoomStartReservation reservation = service.ReserveStart(request).Reservation!;
        service.CancelStart(reservation);
        RoomStartReservationResult retried = service.ReserveStart(request);
        RoomOperationResult confirmed = service.CompleteStart(
            retried.Reservation!, SavedGameId);

        // Assert
        Assert.AreEqual(RoomOperationStatus.Success, retried.Status);
        Assert.AreEqual(SavedGameId, confirmed.Room?.GameId);
    }

    [TestMethod]
    public async Task UpdateRoomConfiguration_Host_UpdatesBothObserversAndStartConfiguration()
    {
        // Arrange
        var service = new RoomsService(
            TimeProvider.System,
            new SessionConnectionRegistry(),
            NullLogger<RoomsService>.Instance);
        PrivateRoom room = CreateRoomWithOpponent(service);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using IAsyncEnumerator<RoomOperationResult> host = service.WatchRoomAsync(
            new RoomObservation(room.Host.UserId, room.RoomId, room.Version), timeout.Token)
            .GetAsyncEnumerator();
        await using IAsyncEnumerator<RoomOperationResult> guest = service.WatchRoomAsync(
            new RoomObservation(room.Opponent!.UserId, room.RoomId, room.Version), timeout.Token)
            .GetAsyncEnumerator();
        Task<bool> hostUpdate = host.MoveNextAsync().AsTask();
        Task<bool> guestUpdate = guest.MoveNextAsync().AsTask();
        var command = new UpdateRoomConfigurationCommand(
            room.RoomId, new LobbyConfiguration(3, 600, 30), room.Version);
        PrivateRoom? persisted = null;

        // Act
        RoomOperationResult updated = service.UpdateRoomConfiguration(command, room.Host.UserId);
        bool[] received = await Task.WhenAll(hostUpdate, guestUpdate);
        RoomStartReservation reservation = service.ReserveStart(
            new RoomStartRequest(room.RoomId, room.Version + 1, room.Host.UserId))
            .Reservation!;
        persisted = reservation.Room;
        RoomOperationResult started = service.CompleteStart(reservation, SavedGameId);

        // Assert
        Assert.IsTrue(received.All(value => value));
        Assert.AreEqual(RoomOperationStatus.Success, updated.Status);
        Assert.AreEqual(room.Version + 1, updated.Room?.Version);
        Assert.AreEqual(updated.Room, host.Current.Room);
        Assert.AreEqual(updated.Room, guest.Current.Room);
        Assert.AreEqual(command.Configuration, persisted?.Configuration);
        Assert.AreEqual(SavedGameId, started.Room?.GameId);
    }

    [TestMethod]
    [DataRow(2)]
    [DataRow(3)]
    public void UpdateRoomConfiguration_NonHost_RejectsAndPreservesRoom(int userId)
    {
        // Arrange
        var service = new RoomsService(
            TimeProvider.System,
            new SessionConnectionRegistry(),
            NullLogger<RoomsService>.Instance);
        PrivateRoom room = CreateRoomWithOpponent(service);
        var command = new UpdateRoomConfigurationCommand(
            room.RoomId, new LobbyConfiguration(3, 600, 30), room.Version);

        // Act
        RoomOperationResult result = service.UpdateRoomConfiguration(command, userId);
        RoomOperationResult current = service.JoinRoom(
            room.Host, new RoomLocator(room.RoomId, string.Empty));

        // Assert
        Assert.AreEqual(RoomOperationStatus.HostRequired, result.Status);
        Assert.AreEqual(room, current.Room);
    }

    [TestMethod]
    [DataRow(2, 600, 30)]
    [DataRow(3, 600, 0)]
    [DataRow(3, -1, 30)]
    public void UpdateRoomConfiguration_InvalidValues_PreservesVersion(
        int captureGoal, int gameSeconds, int turnSeconds)
    {
        // Arrange
        var service = new RoomsService(
            TimeProvider.System,
            new SessionConnectionRegistry(),
            NullLogger<RoomsService>.Instance);
        PrivateRoom room = CreateRoomWithOpponent(service);
        var command = new UpdateRoomConfigurationCommand(
            room.RoomId,
            new LobbyConfiguration(captureGoal, gameSeconds, turnSeconds),
            room.Version);

        // Act
        RoomOperationResult result = service.UpdateRoomConfiguration(command, room.Host.UserId);
        RoomOperationResult current = service.JoinRoom(
            room.Host, new RoomLocator(room.RoomId, string.Empty));

        // Assert
        Assert.AreEqual(RoomOperationStatus.InvalidConfiguration, result.Status);
        Assert.AreEqual(room, current.Room);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(20)]
    public void UpdateRoomConfiguration_IndependentTurnClock_AcceptsWithoutSharedTime(
        int legacySeconds)
    {
        // Arrange
        var service = new RoomsService(TimeProvider.System, new SessionConnectionRegistry(),
            NullLogger<RoomsService>.Instance);
        PrivateRoom room = CreateRoomWithOpponent(service);
        var command = new UpdateRoomConfigurationCommand(room.RoomId,
            new LobbyConfiguration(3, legacySeconds, 30), room.Version);
        // Act
        RoomOperationResult result = service.UpdateRoomConfiguration(command, room.Host.UserId);
        // Assert
        Assert.AreEqual(RoomOperationStatus.Success, result.Status);
        Assert.AreEqual(30, result.Room!.Configuration.TurnTimeSeconds);
    }

    [TestMethod]
    public async Task UpdateRoomConfiguration_UpdateBeforeStart_RejectsStaleStartAndUpdate()
    {
        // Arrange
        var service = new RoomsService(
            TimeProvider.System,
            new SessionConnectionRegistry(),
            NullLogger<RoomsService>.Instance);
        PrivateRoom room = CreateRoomWithOpponent(service);
        var command = new UpdateRoomConfigurationCommand(
            room.RoomId, new LobbyConfiguration(3, 600, 30), room.Version);

        // Act
        RoomOperationResult first = service.UpdateRoomConfiguration(command, room.Host.UserId);
        RoomOperationResult stale = service.UpdateRoomConfiguration(command, room.Host.UserId);
        RoomStartReservationResult start = service.ReserveStart(
            new RoomStartRequest(room.RoomId, room.Version, room.Host.UserId));

        // Assert
        Assert.AreEqual(RoomOperationStatus.Success, first.Status);
        Assert.AreEqual(RoomOperationStatus.VersionMismatch, stale.Status);
        Assert.AreEqual(RoomOperationStatus.VersionMismatch, start.Status);
    }

    [TestMethod]
    public void UpdateRoomConfiguration_StartReservedOrCompleted_RejectsChange()
    {
        // Arrange
        var service = new RoomsService(
            TimeProvider.System,
            new SessionConnectionRegistry(),
            NullLogger<RoomsService>.Instance);
        PrivateRoom room = CreateRoomWithOpponent(service);
        var command = new UpdateRoomConfigurationCommand(
            room.RoomId, new LobbyConfiguration(3, 600, 30), room.Version);

        // Act
        RoomStartReservation reservation = service.ReserveStart(
            new RoomStartRequest(room.RoomId, room.Version, room.Host.UserId))
            .Reservation!;
        RoomOperationResult reserved = service.UpdateRoomConfiguration(command, room.Host.UserId);
        RoomOperationResult started = service.CompleteStart(reservation, SavedGameId);
        RoomOperationResult completed = service.UpdateRoomConfiguration(
            command with { ExpectedVersion = started.Room!.Version }, room.Host.UserId);

        // Assert
        Assert.AreEqual(RoomOperationStatus.StartInProgress, reserved.Status);
        Assert.AreEqual(RoomOperationStatus.StartInProgress, completed.Status);
        Assert.AreEqual(room.Configuration, started.Room?.Configuration);
    }

    [TestMethod]
    public void UpdateRoomConfiguration_IdenticalOrClosedRoom_DoesNotCreateNewVersion()
    {
        // Arrange
        var service = new RoomsService(
            TimeProvider.System,
            new SessionConnectionRegistry(),
            NullLogger<RoomsService>.Instance);
        PrivateRoom room = CreateRoomWithOpponent(service);
        var command = new UpdateRoomConfigurationCommand(
            room.RoomId, room.Configuration, room.Version);

        // Act
        RoomOperationResult unchanged = service.UpdateRoomConfiguration(command, room.Host.UserId);
        service.LeaveRoom(room.Host.UserId, room.RoomId);
        RoomOperationResult closed = service.UpdateRoomConfiguration(command, room.Host.UserId);
        RoomOperationResult unknown = service.UpdateRoomConfiguration(
            command with { RoomId = int.MaxValue }, room.Host.UserId);

        // Assert
        Assert.AreEqual(RoomOperationStatus.Success, unchanged.Status);
        Assert.AreEqual(room, unchanged.Room);
        Assert.AreEqual(RoomOperationStatus.NotFound, closed.Status);
        Assert.AreEqual(RoomOperationStatus.NotFound, unknown.Status);
    }

    private static PrivateRoom CreateRoomWithOpponent(RoomsService service)
    {
        RoomOperationResult created = service.CreatePrivateRoom(
            new LobbyParticipant(1, "Host"),
            new LobbyConfiguration(5, 600, 30));
        var locator = new RoomLocator(0, created.Room!.InvitationCode);
        RoomOperationResult joined = service.JoinRoom(new LobbyParticipant(2, "Opponent"), locator);
        return joined.Room!;
    }

}
