using AtariGo.Server.Data.Entities;
using AtariGo.GameLogic.Models;

namespace AtariGo.Server.Application.Games;

public sealed class GameCommandService
{
    private const int MaximumActionIdLength = 64;

    private readonly GamePersistenceService _store;
    private readonly GameActionProcessor _processor;
    private readonly TimeProvider _timeProvider;
    private readonly IGameSessionRegistry _registry;

    public GameCommandService(GamePersistenceService store, GameActionProcessor processor,
        TimeProvider timeProvider, IGameSessionRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(processor);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(registry);
        _store = store;
        _processor = processor;
        _timeProvider = timeProvider;
        _registry = registry;
    }

    public async Task<GameOperationContext> LoadAsync(long gameId,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(gameId);
        Game game = await _store.LoadAsync(gameId, cancellationToken);
        GameRuntimeState persisted = GamePersistenceService.Read(game);
        GameRuntimeState progress = _registry.GetProgress(gameId, game.StateVersion)
            ?? persisted;
        return new GameOperationContext(game, progress);
    }

    public async Task<GameOperationResult> AdvanceAsync(GameOperationContext operation,
        Func<bool> areBothConnected)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(areBothConnected);
        Game game = operation.Game;
        GameRuntimeState runtime = operation.Progress;
        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        if (runtime.Status == GameRuntimeState.Playing
            && _registry.GetProgress(game.GameId, game.StateVersion) is null)
        {
            GameRuntimeState cancelled = runtime with
            {
                Status = GameRuntimeState.Cancelled,
                EndReason = "BothDisconnected"
            };
            await _store.SaveAsync(game, cancelled, CancellationToken.None);
            return CreateResult(game, cancelled);
        }
        int seconds = game.GameConfiguration.TurnTimeSeconds;
        GameConnectionPresence presence = _registry.GetPresence(game.GameId);
        GameRuntimeState confirmation = GameTurnConfirmationRules.Expire(runtime, now);
        GameRuntimeState resolved = GameConnectionRules.Resolve(confirmation, now, presence);
        GameRuntimeState next = GameClock.Advance(resolved, now, seconds);
        bool turnChanged = next.Board.TurnNumber != resolved.Board.TurnNumber;
        if (areBothConnected())
        {
            next = GameStateTransitions.Start(next, now, seconds);
        }
        if (next.Status == GameRuntimeState.Playing && turnChanged)
        {
            next = GameTurnConfirmationRules.Begin(next, game.StateVersion + 1, now, seconds);
        }
        if (next != runtime)
        {
            await _store.SaveAsync(game, next, CancellationToken.None);
        }
        return CreateResult(game, next);
    }

    public void EnsureReconnectAllowed(GameOperationContext operation, string playerKey)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentException.ThrowIfNullOrWhiteSpace(playerKey);
        StoneColor color = GetPlayerColor(operation.Game, playerKey);
        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        if (GameConnectionRules.HasExpiredGrace(operation.Progress, color, now))
        {
            throw new TimeoutException("The reconnection grace period expired.");
        }
    }

    public Task<GameOperationResult> PlayerConnectedAsync(GameOperationContext operation,
        string playerKey)
    {
        return UpdateConnectionAsync(operation, playerKey, true);
    }

    public Task<GameOperationResult> PlayerDisconnectedAsync(GameOperationContext operation,
        string playerKey)
    {
        return UpdateConnectionAsync(operation, playerKey, false);
    }

    public async Task<GameOperationResult> AcknowledgeTurnAsync(
        GameOperationContext operation, string playerKey, long version)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentException.ThrowIfNullOrWhiteSpace(playerKey);
        StoneColor color = GetPlayerColor(operation.Game, playerKey);
        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        int seconds = operation.Game.GameConfiguration.TurnTimeSeconds;
        GameRuntimeState next = GameTurnConfirmationRules.Acknowledge(
            operation.Progress, color, version, now, seconds);
        if (next != operation.Progress)
        {
            await _store.SaveAsync(operation.Game, next, CancellationToken.None);
        }
        return CreateResult(operation.Game, next);
    }

    private async Task<GameOperationResult> UpdateConnectionAsync(GameOperationContext operation,
        string playerKey, bool connected)
    {
        StoneColor color = GetPlayerColor(operation.Game, playerKey);
        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        GameRuntimeState current = operation.Progress;
        GameRuntimeState next = connected
            ? GameConnectionRules.Reconnect(current, color, now)
            : GameConnectionRules.Disconnect(current, color, now);
        if (next != current)
        {
            GameConnectionPresence presence = _registry.GetPresence(operation.Game.GameId);
            if (!connected)
            {
                presence = color == StoneColor.Black
                    ? presence with { BlackConnected = false }
                    : presence with { WhiteConnected = false };
            }
            next = GameConnectionRules.Resolve(next, now, presence);
            await _store.SaveAsync(operation.Game, next, CancellationToken.None);
        }
        return CreateResult(operation.Game, next);
    }

    private static StoneColor GetPlayerColor(Game game, string playerKey)
    {
        GamePlayer player = game.GamePlayers.Single(participant =>
            GetPlayerKey(participant) == playerKey);
        return player.StoneColor == GameSessionRules.BlackStoneColor
            ? StoneColor.Black : StoneColor.White;
    }

    public async Task<GameOperationResult> ProcessAsync(GameSessionConnection connection,
        GameActionCommand command, Func<bool> areBothConnected)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(areBothConnected);
        if (command.ActionId.Length > MaximumActionIdLength
            || !Guid.TryParseExact(command.ActionId, "N", out _))
        {
            return new GameOperationResult
            {
                GameId = connection.GameId,
                ActionResult = new GameActionResult
                {
                    ActionId = command.ActionId,
                    Rejection = "InvalidAction"
                }
            };
        }
        GameOperationContext operation = await LoadAsync(connection.GameId, CancellationToken.None);
        GameActionContext context = CreateActionContext(operation, connection, command);
        GameActionResult? previous = await _store.FindActionAsync(context);
        GameOperationResult outcome;
        if (previous is not null)
        {
            GameOperationResult advanced = await AdvanceAsync(operation, areBothConnected);
            outcome = advanced with
            {
                ActionResult = previous
            };
        }
        else
        {
            outcome = await ProcessNewActionAsync(operation, context);
        }
        return outcome;
    }

    private async Task<GameOperationResult> ProcessNewActionAsync(GameOperationContext operation,
        GameActionContext context)
    {
        context.Now = _timeProvider.GetUtcNow().UtcDateTime;
        GameActionResult result = _processor.Apply(context);
        if (result.IsAccepted && !context.Command.IsSurrender
            && context.Runtime.Status == GameRuntimeState.Playing)
        {
            context.Runtime = GameTurnConfirmationRules.Begin(context.Runtime,
                operation.Version + 1, context.Now,
                operation.Game.GameConfiguration.TurnTimeSeconds);
        }
        bool stateChanged = context.Runtime != operation.Progress;
        long nextVersion = operation.Version;
        if (stateChanged)
        {
            nextVersion++;
        }
        result = result with
        {
            Version = nextVersion
        };
        await _store.SaveActionAsync(context, result, operation.Progress);
        return CreateResult(operation.Game, context.Runtime) with
        {
            ActionResult = result
        };
    }

    private static GameActionContext CreateActionContext(GameOperationContext operation,
        GameSessionConnection connection, GameActionCommand command)
    {
        GamePlayer player = operation.Game.GamePlayers.Single(participant =>
            GetPlayerKey(participant) == connection.PlayerKey);
        return new GameActionContext
        {
            Command = command,
            Game = operation.Game,
            Runtime = operation.Progress,
            Player = player
        };
    }

    private static GameOperationResult CreateResult(Game game, GameRuntimeState progress)
    {
        return new GameOperationResult
        {
            GameId = game.GameId,
            Progress = progress,
            Version = game.StateVersion
        };
    }

    private static string GetPlayerKey(GamePlayer player)
    {
        return player.UserId.HasValue
            ? GameSessionRules.CreateUserKey(player.UserId.Value)
            : GameSessionRules.CreateGuestKey(player.GuestIdentityId!.Value);
    }
}
