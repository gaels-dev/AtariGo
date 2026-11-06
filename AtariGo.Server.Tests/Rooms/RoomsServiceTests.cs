using Microsoft.VisualStudio.TestTools.UnitTesting;

using AtariGo.Server.Application.Rooms;

namespace AtariGo.Server.Tests.Rooms;

[TestClass]
[TestCategory("Unit")]
public sealed class RoomsServiceTests
{
    private const long SavedGameId = 42;

    [TestMethod]
    public async Task StartGameAsync_ConcurrentRequests_PublishesOnlyConfirmedGame()
    {
        // Arrange
        var service = new RoomsService(TimeProvider.System);
        PrivateRoom room = CreateRoomWithOpponent(service);
        var request = new RoomStartRequest(room.RoomId, room.Version, room.Host.UserId);
        var persist = new TaskCompletionSource<long>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using IAsyncEnumerator<RoomOperationResult> observer = service.WatchRoomAsync(
            new RoomObservation(room.Opponent!.UserId, room.RoomId, room.Version),
            timeout.Token).GetAsyncEnumerator();

        // Act
        Task<bool> nextUpdate = observer.MoveNextAsync().AsTask();
        Task<RoomOperationResult> firstStart = service.StartGameAsync(
            request, (_, _) => persist.Task, timeout.Token);
        RoomOperationResult competingStart = await service.StartGameAsync(
            request, RejectSecondWriteAsync, timeout.Token);
        bool publishedBeforeSaving = nextUpdate.IsCompleted;
        persist.SetResult(SavedGameId);
        RoomOperationResult confirmed = await firstStart;
        bool received = await nextUpdate;
        RoomOperationResult retried = await service.StartGameAsync(
            request, RejectSecondWriteAsync, timeout.Token);

        // Assert
        Assert.IsFalse(publishedBeforeSaving);
        Assert.AreEqual(RoomOperationStatus.StartInProgress, competingStart.Status);
        Assert.IsTrue(received);
        Assert.AreEqual(SavedGameId, confirmed.Room?.GameId);
        Assert.AreEqual(confirmed.Room, observer.Current.Room);
        Assert.AreEqual(confirmed.Room, retried.Room);
    }

    [TestMethod]
    public async Task StartGameAsync_StaleVersionOrOpponent_RejectsStart()
    {
        // Arrange
        var service = new RoomsService(TimeProvider.System);
        PrivateRoom room = CreateRoomWithOpponent(service);
        var stale = new RoomStartRequest(room.RoomId, room.Version - 1, room.Host.UserId);
        var opponent = new RoomStartRequest(room.RoomId, room.Version, room.Opponent!.UserId);

        // Act
        RoomOperationResult staleStart = await service.StartGameAsync(
            stale, RejectSecondWriteAsync, CancellationToken.None);
        RoomOperationResult opponentStart = await service.StartGameAsync(
            opponent, RejectSecondWriteAsync, CancellationToken.None);

        // Assert
        Assert.AreEqual(RoomOperationStatus.VersionMismatch, staleStart.Status);
        Assert.AreEqual(RoomOperationStatus.NotMember, opponentStart.Status);
    }

    [TestMethod]
    public async Task StartGameAsync_PersistenceFails_ReleasesReservationForRetry()
    {
        // Arrange
        var service = new RoomsService(TimeProvider.System);
        PrivateRoom room = CreateRoomWithOpponent(service);
        var request = new RoomStartRequest(room.RoomId, room.Version, room.Host.UserId);

        // Act
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            service.StartGameAsync(request, RejectSecondWriteAsync, CancellationToken.None));
        RoomOperationResult retried = await service.StartGameAsync(
            request, (_, _) => Task.FromResult(SavedGameId), CancellationToken.None);

        // Assert
        Assert.AreEqual(RoomOperationStatus.Success, retried.Status);
        Assert.AreEqual(SavedGameId, retried.Room?.GameId);
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

    private static Task<long> RejectSecondWriteAsync(
        PrivateRoom room,
        CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("Unexpected game persistence.");
    }
}
