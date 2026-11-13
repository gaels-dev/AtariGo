using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using AtariGo.Server.Application.Games;
using AtariGo.Server.Application.Rooms;
using AtariGo.Server.Application.Sessions;
using AtariGo.Server.Data;

namespace AtariGo.Server.Tests.Rooms;

[TestClass]
[TestCategory("Unit")]
public sealed class GameLifecycleServiceTests
{
    [TestMethod]
    public async Task StartGameAsync_PersistenceFails_ClearsTrackedGraphAndReleasesRoom()
    {
        // Arrange
        DbContextOptions<AtariGoDbContext> options = new DbContextOptionsBuilder<AtariGoDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        await using var dbContext = new FailingAtariGoDbContext(options);
        var roomsService = new RoomsService(
            TimeProvider.System,
            new SessionConnectionRegistry(),
            NullLogger<RoomsService>.Instance);
        PrivateRoom room = CreateRoomWithOpponent(roomsService);
        var service = new GameLifecycleService(roomsService, dbContext, TimeProvider.System);
        var request = new RoomStartRequest(room.RoomId, room.Version, room.Host.UserId);

        // Act
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            service.StartGameAsync(request, CancellationToken.None));
        RoomStartReservationResult retry = roomsService.ReserveStart(request);
        roomsService.CancelStart(retry.Reservation!);

        // Assert
        Assert.AreEqual(0, dbContext.ChangeTracker.Entries().Count());
        Assert.AreEqual(RoomOperationStatus.Success, retry.Status);
        Assert.AreEqual(room, retry.Room);
    }

    private static PrivateRoom CreateRoomWithOpponent(RoomsService service)
    {
        RoomOperationResult created = service.CreatePrivateRoom(
            new LobbyParticipant(1, "Host"),
            new LobbyConfiguration(5, 600, 30));
        RoomLocator locator = new RoomLocator(0, created.Room!.InvitationCode);
        RoomOperationResult joined = service.JoinRoom(
            new LobbyParticipant(2, "Opponent"), locator);
        return joined.Room!;
    }

    private sealed class FailingAtariGoDbContext : AtariGoDbContext
    {
        public FailingAtariGoDbContext(DbContextOptions<AtariGoDbContext> options)
            : base(options)
        {
        }

        public override Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Simulated persistence failure.");
        }
    }
}
