using AtariGo.Server.Data.Entities;

namespace AtariGo.Server.Application.Games;

public sealed class GameCommandService
{
    private const int MaximumActionIdLength = 64;

    private readonly GamePersistenceService _store;
    private readonly GameActionProcessor _processor;
    private readonly TimeProvider _timeProvider;

    public GameCommandService(GamePersistenceService store, GameActionProcessor processor,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(processor);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _store = store;
        _processor = processor;
        _timeProvider = timeProvider;
    }

    public async Task<GameOperationContext> LoadAsync(long gameId,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(gameId);
        Game game = await _store.LoadAsync(gameId, cancellationToken);
        return new GameOperationContext(game, GamePersistenceService.Read(game));
    }

    public async Task<GameOperationResult> AdvanceAsync(GameOperationContext operation,
        Func<bool> areBothConnected)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(areBothConnected);
        Game game = operation.Game;
        GameRuntimeState runtime = operation.Progress;
        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        int seconds = game.GameConfiguration.TurnTimeSeconds;
        GameRuntimeState next = GameClock.Advance(runtime, now, seconds);
        if (areBothConnected())
        {
            next = GameStateTransitions.Start(next, now, seconds);
        }
        if (next != runtime)
        {
            await _store.SaveAsync(game, next, CancellationToken.None);
        }
        return CreateResult(game, next);
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
            outcome = advanced with { ActionResult = previous };
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
        bool stateChanged = context.Runtime != operation.Progress;
        long nextVersion = operation.Version;
        if (stateChanged)
        {
            nextVersion++;
        }
        result = result with { Version = nextVersion };
        await _store.SaveActionAsync(context, result, operation.Progress);
        return CreateResult(operation.Game, context.Runtime) with { ActionResult = result };
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
