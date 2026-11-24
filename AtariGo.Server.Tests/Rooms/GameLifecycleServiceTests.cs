using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using AtariGo.Server.Application.Games;
using AtariGo.Server.Application.Matchmaking;
using AtariGo.Server.Application.Reports;
using AtariGo.Server.Application.Rooms;
using AtariGo.Server.Application.Sessions;
using AtariGo.Server.Data;
using AtariGo.Server.Data.Entities;
using AtariGo.Server.Tests.Games;
using AtariGo.Server.Tests.Reports;

namespace AtariGo.Server.Tests.Rooms;

[TestClass]
[TestCategory("Unit")]
public sealed class GameLifecycleServiceTests
{
    [TestMethod]
    public async Task StartPublicGameAsync_TwoGuests_PersistsSharedGameAndInitialStateAsync()
    {
        // Arrange
        await using GameplayTestDatabase database = await GameplayTestDatabase.CreateAsync();
        await using AtariGoDbContext context = database.CreateContext();
        RoomsService rooms = new RoomsService(database.Time,
            new SessionConnectionRegistry(), NullLogger<RoomsService>.Instance);
        GameLifecycleService service = new GameLifecycleService(rooms, context,
            database.Time, new AllowAllAccountSanctionService(),
            NullLogger<GameLifecycleService>.Instance);
        Guid blackId = Guid.NewGuid();
        Guid whiteId = Guid.NewGuid();
        PublicMatchStartRequest request = new PublicMatchStartRequest(
            new MatchmakingPlayer
            {
                PlayerKey = blackId.ToString(),
                GuestName = "Black guest",
                GuestIdentityId = blackId
            },
            new MatchmakingPlayer
            {
                PlayerKey = whiteId.ToString(),
                GuestName = "White guest",
                GuestIdentityId = whiteId
            },
            new LobbyConfiguration(5, 600, 30));

        // Act
        long gameId = await service.StartPublicGameAsync(request, CancellationToken.None);
        await using AtariGoDbContext verification = database.CreateContext();
        Game game = await verification.Games.AsNoTracking()
            .Include(current => current.GameConfiguration)
            .Include(current => current.GamePlayers)
            .SingleAsync(current => current.GameId == gameId);

        // Assert
        Assert.AreNotEqual(database.GameId, gameId);
        Assert.AreEqual("Public", game.GameType);
        Assert.IsNull(game.RoomId);
        Assert.AreEqual(GameRuntimeState.Waiting, game.State);
        Assert.AreEqual(5, game.GameConfiguration.CaptureGoal);
        Assert.AreEqual(600, game.GameConfiguration.GameTimeSeconds);
        Assert.AreEqual(30, game.GameConfiguration.TurnTimeSeconds);
        Assert.IsNotNull(game.BoardStateJson);
        Assert.IsNotNull(game.TurnDeadlineUtc);
        Assert.AreEqual(2, game.GamePlayers.Count);
        GamePlayer black = game.GamePlayers.Single(player => player.StoneColor == "Black");
        GamePlayer white = game.GamePlayers.Single(player => player.StoneColor == "White");
        Assert.AreEqual(blackId, black.GuestIdentityId);
        Assert.AreEqual(whiteId, white.GuestIdentityId);
        Assert.AreEqual((byte)1, black.TurnOrder);
        Assert.AreEqual((byte)2, white.TurnOrder);
    }

    [TestMethod]
    public async Task StartPublicGameAsync_SuspendedPlayer_DoesNotPersistGameAsync()
    {
        // Arrange
        DbContextOptions<AtariGoDbContext> options = new DbContextOptionsBuilder<AtariGoDbContext>()
            .UseSqlite("Data Source=:memory:").Options;
        await using FailingAtariGoDbContext context = new FailingAtariGoDbContext(options);
        RoomsService rooms = new RoomsService(TimeProvider.System,
            new SessionConnectionRegistry(), NullLogger<RoomsService>.Instance);
        GameLifecycleService service = new GameLifecycleService(rooms, context,
            TimeProvider.System, new AllowAllAccountSanctionService { IsSuspended = true },
            NullLogger<GameLifecycleService>.Instance);
        PublicMatchStartRequest request = new PublicMatchStartRequest(
            new MatchmakingPlayer { PlayerKey = "1", UserId = 1 },
            new MatchmakingPlayer
            {
                PlayerKey = "guest",
                GuestName = "Guest",
                GuestIdentityId = Guid.NewGuid()
            },
            new LobbyConfiguration(5, 600, 30));

        // Act
        await Assert.ThrowsExactlyAsync<AccountSuspendedException>(() =>
            service.StartPublicGameAsync(request, CancellationToken.None));

        // Assert
        Assert.AreEqual(0, context.ChangeTracker.Entries().Count());
    }

    [TestMethod]
    public async Task StartGameAsync_SuspendedParticipant_ReleasesRoomReservation()
    {
        // Arrange
        DbContextOptions<AtariGoDbContext> options = new DbContextOptionsBuilder<AtariGoDbContext>()
            .UseSqlite("Data Source=:memory:").Options;
        await using var dbContext = new FailingAtariGoDbContext(options);
        var roomsService = new RoomsService(TimeProvider.System,
            new SessionConnectionRegistry(), NullLogger<RoomsService>.Instance);
        PrivateRoom room = CreateRoomWithOpponent(roomsService);
        var sanctions = new AllowAllAccountSanctionService { IsSuspended = true };
        var service = new GameLifecycleService(roomsService, dbContext,
            TimeProvider.System, sanctions, NullLogger<GameLifecycleService>.Instance);
        var request = new RoomStartRequest(room.RoomId, room.Version, room.Host.UserId);

        // Act
        RoomOperationResult result = await service.StartGameAsync(
            request, CancellationToken.None);
        RoomStartReservationResult retry = roomsService.ReserveStart(request);
        roomsService.CancelStart(retry.Reservation!);

        // Assert
        Assert.AreEqual(RoomOperationStatus.AccountRestricted, result.Status);
        Assert.AreEqual(RoomOperationStatus.Success, retry.Status);
        Assert.IsNull(result.Room?.GameId);
    }

    [TestMethod]
    public async Task StartGameAsync_PersistenceFails_ClearsTrackedGraphAndReleasesRoomAsync()
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
        var service = new GameLifecycleService(roomsService, dbContext,
            TimeProvider.System, new AllowAllAccountSanctionService(),
            NullLogger<GameLifecycleService>.Instance);
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
