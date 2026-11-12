using System;

using Microsoft.EntityFrameworkCore;

using AtariGo.Server.Data;
using AtariGo.Server.Data.Entities;

namespace AtariGo.Server.Application.Games;

public sealed class GameSessionCatalog : IGameSessionCatalog
{
    private readonly AtariGoDbContext _dbContext;

    public GameSessionCatalog(AtariGoDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task<GameSessionDefinition?> FindForPlayerAsync(
        GameSessionPlayerRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        Game? game = await FindActiveGameAsync(request.GameId, cancellationToken);
        if (game is null || !HasValidParticipants(game))
        {
            return null;
        }

        GamePlayer? player = game.GamePlayers.FirstOrDefault(
            gamePlayer => IsPlayer(
                gamePlayer,
                request.UserId,
                request.GuestIdentityId));
        if (player is null)
        {
            return null;
        }

        GamePlayer opponent = game.GamePlayers.First(
            gamePlayer => gamePlayer.GamePlayerId != player.GamePlayerId);
        if (game.BoardStateJson is null)
        {
            return null;
        }
        return CreateDefinition(game, player, opponent) with
        {
            Progress = GamePersistenceService.Read(game),
            Version = game.StateVersion
        };
    }

    private Task<Game?> FindActiveGameAsync(
        long gameId,
        CancellationToken cancellationToken)
    {
        return _dbContext.Games
            .AsNoTracking()
            .Include(game => game.GameConfiguration)
            .Include(game => game.GamePlayers)
                .ThenInclude(gamePlayer => gamePlayer.User)
            .SingleOrDefaultAsync(
                game => game.GameId == gameId,
                cancellationToken);
    }

    private static bool HasValidParticipants(Game game)
    {
        return game.GamePlayers.Count == GameSessionRules.PlayerCount
            && game.GamePlayers.All(IsValidParticipant);
    }

    private static bool IsValidParticipant(GamePlayer player)
    {
        return player.UserId.HasValue && player.User is not null
            || !player.UserId.HasValue
                && player.GuestIdentityId.HasValue
                && !string.IsNullOrWhiteSpace(player.GuestName);
    }

    private static bool IsPlayer(
        GamePlayer gamePlayer,
        int? userId,
        Guid? guestIdentityId)
    {
        if (userId.HasValue)
        {
            return gamePlayer.UserId == userId;
        }

        return guestIdentityId.HasValue
            && gamePlayer.GuestIdentityId == guestIdentityId;
    }

    private static GameSessionDefinition CreateDefinition(
        Game game,
        GamePlayer player,
        GamePlayer opponent)
    {
        return new GameSessionDefinition
        {
            GameId = game.GameId,
            CaptureGoal = game.GameConfiguration.CaptureGoal,
            GameTimeSeconds = game.GameConfiguration.GameTimeSeconds,
            TurnTimeSeconds = game.GameConfiguration.TurnTimeSeconds,
            Player = CreateParticipant(player),
            Opponent = CreateParticipant(opponent)
        };
    }

    private static GameSessionParticipant CreateParticipant(GamePlayer player)
    {
        string playerKey = player.UserId.HasValue
            ? GameSessionRules.CreateUserKey(player.UserId.Value)
            : GameSessionRules.CreateGuestKey(player.GuestIdentityId!.Value);
        string displayName = player.User?.UserName ?? player.GuestName!;
        return new GameSessionParticipant(playerKey, displayName, player.StoneColor);
    }
}
