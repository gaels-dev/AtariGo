using AtariGo.Server.Application.Rooms;
using AtariGo.Server.Application.Matchmaking;
using AtariGo.Server.Data;
using AtariGo.Server.Data.Entities;

namespace AtariGo.Server.Application.Games;

public sealed class GameLifecycleService : IGameLifecycleService
{
    private const byte HostTurnOrder = 1;
    private const byte OpponentTurnOrder = 2;

    private readonly IRoomsService _roomsService;
    private readonly AtariGoDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public GameLifecycleService(
        IRoomsService roomsService,
        AtariGoDbContext dbContext,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(roomsService);
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _roomsService = roomsService;
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task<RoomOperationResult> StartGameAsync(
        RoomStartRequest startRequest,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RoomStartReservationResult result = _roomsService.ReserveStart(startRequest);
        if (result.Status != RoomOperationStatus.Success)
        {
            return new RoomOperationResult(result.Status, result.Room);
        }

        if (result.Reservation is null)
        {
            return new RoomOperationResult(RoomOperationStatus.Success, result.Room);
        }

        long gameId;
        try
        {
            gameId = await PersistGameAsync(result.Reservation.Room, CancellationToken.None);
        }
        catch
        {
            _dbContext.ChangeTracker.Clear();
            _roomsService.CancelStart(result.Reservation);
            throw;
        }

        RoomOperationResult completed = CompleteStartWithRetry(
            result.Reservation, gameId);
        if (completed.Status != RoomOperationStatus.Success)
        {
            throw new InvalidOperationException(
                "The persisted game could not be confirmed in its reserved room.");
        }

        return completed;
    }

    private RoomOperationResult CompleteStartWithRetry(
        RoomStartReservation reservation,
        long gameId)
    {
        RoomOperationResult completed;
        try
        {
            completed = _roomsService.CompleteStart(reservation, gameId);
        }
        catch (InvalidOperationException)
        {
            completed = _roomsService.CompleteStart(reservation, gameId);
        }

        return completed;
    }

    public async Task<long> StartPublicGameAsync(
        PublicMatchStartRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        var game = new Game
        {
            GameConfiguration = CreateConfiguration(request.Configuration),
            GameType = DatabaseValues.GameTypes.Public,
            State = DatabaseValues.GameStates.InProgress,
            StartedAt = now
        };
        game.GamePlayers.Add(CreateGamePlayer(
            request.BlackPlayer,
            DatabaseValues.StoneColors.Black,
            HostTurnOrder,
            now));
        game.GamePlayers.Add(CreateGamePlayer(
            request.WhitePlayer,
            DatabaseValues.StoneColors.White,
            OpponentTurnOrder,
            now));

        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return game.GameId;
    }

    private async Task<long> PersistGameAsync(
        PrivateRoom lobby,
        CancellationToken cancellationToken)
    {
        LobbyParticipant opponent = lobby.Opponent
            ?? throw new InvalidOperationException("The room requires an opponent.");
        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        var room = new Room
        {
            InvitationCode = lobby.InvitationCode,
            CreatorId = lobby.Host.UserId,
            State = DatabaseValues.RoomStates.Active,
            CreatedAt = now
        };
        var game = new Game
        {
            Room = room,
            GameConfiguration = CreateConfiguration(lobby.Configuration),
            GameType = DatabaseValues.GameTypes.Private,
            State = DatabaseValues.GameStates.InProgress,
            StartedAt = now
        };
        game.GamePlayers.Add(new GamePlayer
        {
            UserId = lobby.Host.UserId,
            StoneColor = DatabaseValues.StoneColors.Black,
            TurnOrder = HostTurnOrder,
            JoinedAt = now
        });
        game.GamePlayers.Add(new GamePlayer
        {
            UserId = opponent.UserId,
            StoneColor = DatabaseValues.StoneColors.White,
            TurnOrder = OpponentTurnOrder,
            JoinedAt = now
        });

        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return game.GameId;
    }

    private static GameConfiguration CreateConfiguration(LobbyConfiguration configuration)
    {
        return new GameConfiguration
        {
            CaptureGoal = checked((byte)configuration.CaptureGoal),
            GameTimeSeconds = configuration.GameTimeSeconds,
            TurnTimeSeconds = configuration.TurnTimeSeconds
        };
    }

    private static GamePlayer CreateGamePlayer(
        MatchmakingPlayer player,
        string stoneColor,
        byte turnOrder,
        DateTime joinedAt)
    {
        return new GamePlayer
        {
            UserId = player.UserId,
            GuestName = player.GuestName,
            GuestIdentityId = player.GuestIdentityId,
            StoneColor = stoneColor,
            TurnOrder = turnOrder,
            JoinedAt = joinedAt
        };
    }
}
