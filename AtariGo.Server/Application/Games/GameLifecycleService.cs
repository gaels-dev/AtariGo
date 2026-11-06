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

    public Task<RoomOperationResult> StartGameAsync(
        RoomStartRequest request,
        CancellationToken cancellationToken)
    {
        return _roomsService.StartGameAsync(request, PersistGameAsync, cancellationToken);
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
