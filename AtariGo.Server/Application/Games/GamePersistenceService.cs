using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using AtariGo.GameLogic.Models;
using AtariGo.Server.Data;
using AtariGo.Server.Data.Entities;

namespace AtariGo.Server.Application.Games;

public sealed class GamePersistenceService
{
    private readonly AtariGoDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public GamePersistenceService(AtariGoDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public Task<Game> LoadAsync(long gameId, CancellationToken cancellationToken)
    {
        return _dbContext.Games.Include(game => game.GamePlayers)
            .Include(game => game.GameConfiguration)
            .SingleAsync(game => game.GameId == gameId, cancellationToken);
    }

    public static GameRuntimeState Read(Game game)
    {
        if (game.BoardStateJson is null || !game.TurnDeadlineUtc.HasValue)
        {
            throw new InvalidOperationException("The persisted game state is missing.");
        }
        GameState board = JsonSerializer.Deserialize<GameState>(game.BoardStateJson)
            ?? throw new InvalidOperationException("The persisted board is missing.");
        return new GameRuntimeState
        {
            Board = board,
            Status = game.State,
            DeadlineUtc = game.TurnDeadlineUtc.Value,
            EndReason = game.EndReason ?? string.Empty
        };
    }

    public static void Initialize(Game game, int goal, DateTime now)
    {
        game.BoardStateJson = JsonSerializer.Serialize(new GameState { CaptureGoal = goal });
        game.State = GameRuntimeState.Waiting;
        game.TurnDeadlineUtc = now.AddSeconds(GameRuntimeState.ConnectionGraceSeconds);
    }

    public async Task<GameActionResult?> FindActionAsync(GameActionContext context)
    {
        GameEvent? entry = await _dbContext.GameEvents.SingleOrDefaultAsync(gameEvent =>
            gameEvent.GameId == context.Game.GameId
            && gameEvent.GamePlayerId == context.Player.GamePlayerId
            && gameEvent.ActionId == context.Command.ActionId);
        if (entry is null)
        {
            return null;
        }
        string response = entry.ActionResultJson
            ?? throw new InvalidOperationException("The persisted action response is missing.");
        GameActionResult result = JsonSerializer.Deserialize<GameActionResult>(response)
            ?? throw new InvalidOperationException("The persisted action result is missing.");
        if (entry.ActionRequestJson != JsonSerializer.Serialize(context.Command))
        {
            result = result with { IsAccepted = false, Rejection = "ActionConflict" };
        }
        return result;
    }

    public void RecordAction(GameActionContext context, GameActionResult result)
    {
        string eventType = "ActionRejected";
        if (result.IsAccepted)
        {
            eventType = context.Command.IsSurrender ? "Surrender" : "Move";
        }
        _dbContext.GameEvents.Add(new GameEvent
        {
            GameId = context.Game.GameId,
            GamePlayerId = context.Player.GamePlayerId,
            EventType = eventType,
            Description = result.Rejection,
            CreatedAt = context.Now,
            ActionId = context.Command.ActionId,
            ActionRequestJson = JsonSerializer.Serialize(context.Command),
            ActionResultJson = JsonSerializer.Serialize(result)
        });
    }

    public async Task SaveAsync(Game game, GameRuntimeState state,
        CancellationToken cancellationToken)
    {
        AddTransitionEvent(game, state);
        game.StateVersion++;
        game.BoardStateJson = JsonSerializer.Serialize(state.Board);
        game.TurnDeadlineUtc = state.DeadlineUtc;
        game.State = state.Status;
        game.EndReason = state.IsTerminal ? state.EndReason : null;
        UpdateParticipants(game, state);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private void AddTransitionEvent(Game game, GameRuntimeState next)
    {
        GameRuntimeState previous = Read(game);
        string? eventType = null;
        if (next.IsTerminal && !previous.IsTerminal)
        {
            eventType = "GameFinished";
        }
        else if (previous.Status != next.Status)
        {
            eventType = "GameStarted";
        }
        else if (previous.Board.TurnNumber != next.Board.TurnNumber
            && previous.Board.LastMove == next.Board.LastMove)
        {
            eventType = "Timeout";
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
            bool isBlack = player.StoneColor == GameSessionRules.BlackStoneColor;
            StoneColor color = isBlack ? StoneColor.Black : StoneColor.White;
            player.CapturedStones = isBlack ? state.Board.BlackCaptures : state.Board.WhiteCaptures;
            if (state.Status == GameRuntimeState.Playing && color == state.Board.CurrentTurn)
            {
                game.CurrentTurnGamePlayerId = player.GamePlayerId;
            }
            if (state.Status == GameRuntimeState.Completed)
            {
                player.Result = GetResult(state, color);
            }
        }
        if (state.IsTerminal)
        {
            game.CurrentTurnGamePlayerId = null;
        }
    }

    private static string GetResult(GameRuntimeState state, StoneColor color)
    {
        if (color == state.Board.Winner)
        {
            return DatabaseValues.GamePlayerResults.Won;
        }
        return state.EndReason == "Surrender" ? "Surrendered" : "Lost";
    }
}
