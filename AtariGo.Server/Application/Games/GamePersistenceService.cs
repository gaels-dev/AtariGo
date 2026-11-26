using Microsoft.EntityFrameworkCore;

using AtariGo.GameLogic.Models;
using AtariGo.Server.Data;
using AtariGo.Server.Data.Entities;

namespace AtariGo.Server.Application.Games;

public sealed class GamePersistenceService
{
    private readonly AtariGoDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly IGameSessionRegistry _registry;
    private readonly GameReplayService _replay;

    public GamePersistenceService(AtariGoDbContext dbContext, TimeProvider timeProvider,
        IGameSessionRegistry registry, GameReplayService replay)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(replay);
        _dbContext = dbContext;
        _timeProvider = timeProvider;
        _registry = registry;
        _replay = replay;
    }

    public Task<Game> LoadAsync(long gameId, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(gameId);
        return _dbContext.Games.Include(game => game.GamePlayers)
            .Include(game => game.GameConfiguration)
            .SingleAsync(game => game.GameId == gameId, cancellationToken);
    }

    public async Task<GameRuntimeState> ReadAsync(Game game, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(game);
        GameRuntimeState? progress = _registry.GetProgress(game.GameId, game.StateVersion);
        if (progress is not null)
        {
            return progress;
        }
        List<Move> moves = await _dbContext.Moves.AsNoTracking()
            .Where(move => move.GamePlayer.GameId == game.GameId)
            .OrderBy(move => move.TurnNumber).ToListAsync(cancellationToken);
        return new GameRuntimeState
        {
            Board = _replay.Reconstruct(game, moves),
            Status = game.State,
            DeadlineUtc = DateTime.MaxValue,
            EndReason = game.EndReason ?? string.Empty
        };
    }

    public async Task SaveActionAsync(GameActionContext context, GameActionResult result,
        GameRuntimeState previous)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(previous);
        if (result.IsAccepted && !context.Command.IsSurrender)
        {
            AddMove(context, previous.Board);
        }
        if (context.Runtime != previous)
        {
            await SaveAsync(context.Game, context.Runtime, CancellationToken.None);
        }
        else
        {
            await _dbContext.SaveChangesAsync(CancellationToken.None);
            _registry.StoreProgress(context.Game.GameId, context.Runtime,
                context.Game.StateVersion);
        }
        _registry.RememberAction(context, result);
    }

    public async Task SaveAsync(Game game, GameRuntimeState state,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(state);
        if (state.IsTerminal && game.FinishedAt is null)
        {
            game.FinishedAt = _timeProvider.GetUtcNow().UtcDateTime;
        }
        AddTransitionEvent(game, state);
        game.StateVersion++;
        game.State = state.Status;
        game.EndReason = null;
        if (state.IsTerminal)
        {
            game.EndReason = state.EndReason;
        }
        UpdateParticipants(game, state);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _registry.StoreProgress(game.GameId, state, game.StateVersion);
    }

    public async Task CloseInterruptedGamesAsync(CancellationToken cancellationToken)
    {
        List<Game> games = await _dbContext.Games.Include(game => game.GamePlayers)
            .Where(game => game.State == GameRuntimeState.Waiting
                || game.State == GameRuntimeState.Playing).ToListAsync(cancellationToken);
        foreach (Game game in games)
        {
            if (_registry.GetProgress(game.GameId, game.StateVersion) is not null)
            {
                continue;
            }
            CloseInterruptedGame(game);
        }
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private void CloseInterruptedGame(Game game)
    {
        GameRuntimeState cancelled = new GameRuntimeState
        {
            Status = GameRuntimeState.Cancelled,
            EndReason = DatabaseValues.GameEndReasons.ServerRestart
        };
        AddTransitionEvent(game, cancelled);
        game.State = cancelled.Status;
        game.EndReason = cancelled.EndReason;
        game.FinishedAt = _timeProvider.GetUtcNow().UtcDateTime;
        game.StateVersion++;
        foreach (GamePlayer player in game.GamePlayers)
        {
            player.Result = DatabaseValues.GamePlayerResults.Abandoned;
        }
    }

    private void AddMove(GameActionContext context, GameState previous)
    {
        GameState next = context.Runtime.Board;
        int before = previous.BlackCaptures + previous.WhiteCaptures;
        int after = next.BlackCaptures + next.WhiteCaptures;
        _dbContext.Moves.Add(new Move
        {
            GamePlayerId = context.Player.GamePlayerId,
            TurnNumber = next.TurnNumber - 1,
            Row = checked((byte)context.Command.Row),
            Column = checked((byte)context.Command.Column),
            PlayedAt = context.Now,
            CapturedStones = after - before
        });
    }

    private void AddTransitionEvent(Game game, GameRuntimeState next)
    {
        string? eventType = null;
        bool wasTerminal = game.State is GameRuntimeState.Completed or GameRuntimeState.Cancelled;
        if (next.IsTerminal && !wasTerminal)
        {
            eventType = "GameFinished";
        }
        else if (game.State != next.Status)
        {
            eventType = "GameStarted";
        }
        else
        {
            GameRuntimeState? previous = _registry.GetProgress(game.GameId, game.StateVersion);
            if (previous is not null && previous.Board.TurnNumber != next.Board.TurnNumber
                && previous.Board.LastMove == next.Board.LastMove)
            {
                eventType = "Timeout";
            }
        }
        if (eventType is not null)
        {
            _dbContext.GameEvents.Add(new GameEvent
            {
                GameId = game.GameId,
                EventType = eventType,
                Description = next.EndReason,
                CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
            });
        }
    }

    private static void UpdateParticipants(Game game, GameRuntimeState state)
    {
        foreach (GamePlayer player in game.GamePlayers)
        {
            StoneColor color = StoneColor.White;
            player.CapturedStones = state.Board.WhiteCaptures;
            if (player.StoneColor == GameSessionRules.BlackStoneColor)
            {
                color = StoneColor.Black;
                player.CapturedStones = state.Board.BlackCaptures;
            }
            if (state.Status == GameRuntimeState.Completed)
            {
                player.Result = GetResult(state, color);
            }
            else if (state.IsTerminal)
            {
                player.Result = DatabaseValues.GamePlayerResults.Abandoned;
            }
        }
    }

    private static string GetResult(GameRuntimeState state, StoneColor color)
    {
        if (color == state.Board.Winner)
        {
            return DatabaseValues.GamePlayerResults.Won;
        }
        if (state.EndReason == DatabaseValues.GameEndReasons.Surrender)
        {
            return DatabaseValues.GamePlayerResults.Surrendered;
        }
        return DatabaseValues.GamePlayerResults.Lost;
    }
}
