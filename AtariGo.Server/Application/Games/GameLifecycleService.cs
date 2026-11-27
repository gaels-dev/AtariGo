using AtariGo.Server.Application.Rooms;
using AtariGo.Server.Application.Matchmaking;
using AtariGo.Server.Application.Reports;
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
    private readonly IAccountSanctionService _sanctions;
    private readonly IGameSessionRegistry _registry;

    public GameLifecycleService(
        IRoomsService roomsService,
        AtariGoDbContext dbContext,
        TimeProvider timeProvider,
        IAccountSanctionService sanctions,
        IGameSessionRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(roomsService);
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(sanctions);
        ArgumentNullException.ThrowIfNull(registry);

        _roomsService = roomsService;
        _dbContext = dbContext;
        _timeProvider = timeProvider;
        _sanctions = sanctions;
        _registry = registry;
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
            if (await HasSuspendedPlayerAsync(result.Reservation.Room.Host.UserId,
                result.Reservation.Room.Opponent?.UserId, cancellationToken))
            {
                _roomsService.CancelStart(result.Reservation);
                return new RoomOperationResult(
                    RoomOperationStatus.AccountRestricted, result.Room);
            }
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

    public async Task<long> StartPublicGameAsync(
        PublicMatchStartRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (await HasSuspendedPlayerAsync(request.BlackPlayer.UserId,
            request.WhitePlayer.UserId, cancellationToken))
        {
            throw new AccountSuspendedException();
        }
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
            HostTurnOrder,
            now));
        game.GamePlayers.Add(CreateGamePlayer(
            request.WhitePlayer,
            OpponentTurnOrder,
            now));

        _dbContext.Games.Add(game);
        game.State = GameRuntimeState.Waiting;
        await _dbContext.SaveChangesAsync(cancellationToken);
        RegisterProgress(game, now);
        return game.GameId;
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

    private async Task<bool> HasSuspendedPlayerAsync(int? firstUserId,
        int? secondUserId, CancellationToken cancellationToken)
    {
        if (firstUserId is int first
            && await _sanctions.HasActiveSuspensionAsync(first, cancellationToken))
        {
            return true;
        }
        return secondUserId is int second
            && await _sanctions.HasActiveSuspensionAsync(second, cancellationToken);
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
        game.State = GameRuntimeState.Waiting;
        await _dbContext.SaveChangesAsync(cancellationToken);
        RegisterProgress(game, now);
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

    private void RegisterProgress(Game game, DateTime now)
    {
        GameRuntimeState progress = GameRuntimeState.CreateInitial(
            game.GameConfiguration.CaptureGoal, now);
        _registry.StoreProgress(game.GameId, progress, game.StateVersion);
    }

    private static GamePlayer CreateGamePlayer(
        MatchmakingPlayer player,
        byte turnOrder,
        DateTime joinedAt)
    {
        return new GamePlayer
        {
            UserId = player.UserId,
            GuestName = player.GuestName,
            GuestIdentityId = player.GuestIdentityId,
            StoneColor = turnOrder == HostTurnOrder
                ? DatabaseValues.StoneColors.Black : DatabaseValues.StoneColors.White,
            TurnOrder = turnOrder,
            JoinedAt = joinedAt
        };
    }
}
