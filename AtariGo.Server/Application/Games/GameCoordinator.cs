using AtariGo.Server.Data;
using AtariGo.Server.Data.Entities;

namespace AtariGo.Server.Application.Games;

public sealed class GameCoordinator : IDisposable
{
    // Fixed stripes bound memory while serializing every operation for a given game.
    private const int GateCount = 128;
    private const int MaximumActionIdLength = 64;
    private readonly SemaphoreSlim[] _gates = Enumerable.Range(0, GateCount)
        .Select(_ => new SemaphoreSlim(1, 1)).ToArray();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IGameSessionRegistry _registry;
    private readonly TimeProvider _timeProvider;

    public GameCoordinator(IServiceScopeFactory scopeFactory,
        IGameSessionRegistry registry, TimeProvider timeProvider)
    {
        _scopeFactory = scopeFactory;
        _registry = registry;
        _timeProvider = timeProvider;
    }

    public async Task<GameSessionConnection> ConnectAsync(GameSessionDefinition definition,
        CancellationToken cancellationToken)
    {
        SemaphoreSlim gate = GetGate(definition.GameId);
        await gate.WaitAsync(cancellationToken);
        try
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            GamePersistenceService store = scope.ServiceProvider
                .GetRequiredService<GamePersistenceService>();
            Game game = await store.LoadAsync(definition.GameId, cancellationToken);
            GameRuntimeState runtime = GamePersistenceService.Read(game);
            GameSessionConnection connection = _registry.Connect(definition with
            {
                Progress = runtime,
                Version = game.StateVersion
            });
            try
            {
                await StartOrAdvanceAsync(store, game);
                return connection;
            }
            catch
            {
                _registry.Disconnect(connection);
                throw;
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task ProcessAsync(GameSessionConnection connection,
        GameActionCommand command, CancellationToken cancellationToken)
    {
        SemaphoreSlim gate = GetGate(connection.GameId);
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (!_registry.IsCurrent(connection))
            {
                return;
            }
            using IServiceScope scope = _scopeFactory.CreateScope();
            GameActionResult result = await ProcessActionAsync(scope, connection, command);
            _registry.PublishActionResult(connection, result);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task AdvanceAsync(long gameId, CancellationToken cancellationToken)
    {
        SemaphoreSlim gate = GetGate(gameId);
        await gate.WaitAsync(cancellationToken);
        try
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            GamePersistenceService store = scope.ServiceProvider
                .GetRequiredService<GamePersistenceService>();
            Game game = await store.LoadAsync(gameId, cancellationToken);
            await StartOrAdvanceAsync(store, game);
        }
        finally
        {
            gate.Release();
        }
    }

    public void Disconnect(GameSessionConnection connection)
    {
        _registry.Disconnect(connection);
    }

    public ChatMessageSubmissionResult PublishChatMessage(
        GameSessionConnection connection, string text)
    {
        return _registry.PublishChatMessage(connection, text);
    }

    public void Dispose()
    {
        foreach (SemaphoreSlim gate in _gates)
        {
            gate.Dispose();
        }
    }

    private SemaphoreSlim GetGate(long gameId)
    {
        return _gates[(int)((ulong)gameId % GateCount)];
    }

    private async Task<GameActionResult> ProcessActionAsync(IServiceScope scope,
        GameSessionConnection connection, GameActionCommand command)
    {
        if (command.ActionId.Length > MaximumActionIdLength
            || !Guid.TryParseExact(command.ActionId, "N", out _))
        {
            return new GameActionResult
            {
                ActionId = command.ActionId,
                Rejection = "InvalidAction"
            };
        }
        AtariGoDbContext database = scope.ServiceProvider.GetRequiredService<AtariGoDbContext>();
        GamePersistenceService store = scope.ServiceProvider
            .GetRequiredService<GamePersistenceService>();
        Game game = await store.LoadAsync(connection.GameId, CancellationToken.None);
        GameActionContext context = CreateContext(connection, command, game);
        GameActionResult? previous = await store.FindActionAsync(context);
        if (previous is not null)
        {
            await StartOrAdvanceAsync(store, game);
            return previous;
        }
        GameRuntimeState original = context.Runtime;
        GameActionResult result = EvaluateAction(context,
            scope.ServiceProvider.GetRequiredService<GameActionProcessor>());
        bool stateChanged = context.Runtime != original;
        long nextVersion = game.StateVersion;
        if (stateChanged)
        {
            nextVersion++;
        }
        result = result with { Version = nextVersion };
        store.RecordAction(context, result);
        if (stateChanged)
        {
            await SaveTransitionAsync(store, game, context.Runtime);
        }
        else
        {
            await database.SaveChangesAsync(CancellationToken.None);
            _registry.PublishProgress(game.GameId, context.Runtime, game.StateVersion);
        }
        return result;
    }

    private GameActionResult EvaluateAction(GameActionContext context,
        GameActionProcessor processor)
    {
        // Decide after all reads without awaiting between the timestamp and rules.
        context.Now = _timeProvider.GetUtcNow().UtcDateTime;
        GameRuntimeState current = context.Runtime;
        context.Runtime = GameClock.Advance(current, context.Now,
            context.Game.GameConfiguration.TurnTimeSeconds);
        if (context.Runtime != current && !context.Command.IsSurrender)
        {
            return new GameActionResult
            {
                ActionId = context.Command.ActionId,
                Rejection = context.Runtime.IsTerminal ? "Finished" : "StaleVersion"
            };
        }
        return processor.Apply(context);
    }

    private static GameActionContext CreateContext(GameSessionConnection connection,
        GameActionCommand command, Game game)
    {
        GamePlayer player = game.GamePlayers.Single(participant =>
            GetPlayerKey(participant) == connection.PlayerKey);
        return new GameActionContext
        {
            Command = command,
            Game = game,
            Runtime = GamePersistenceService.Read(game),
            Player = player
        };
    }

    private async Task StartOrAdvanceAsync(GamePersistenceService store,
        Game game)
    {
        GameRuntimeState runtime = GamePersistenceService.Read(game);
        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        int seconds = game.GameConfiguration.TurnTimeSeconds;
        GameRuntimeState next = GameClock.Advance(runtime, now, seconds);
        if (next.Status == GameRuntimeState.Waiting && _registry.AreBothConnected(game.GameId))
        {
            next = next with
            {
                Status = GameRuntimeState.Playing,
                DeadlineUtc = now.AddSeconds(seconds)
            };
        }
        if (next != runtime)
        {
            await SaveTransitionAsync(store, game, next);
        }
        else
        {
            _registry.PublishProgress(game.GameId, runtime, game.StateVersion);
        }
    }

    private async Task SaveTransitionAsync(GamePersistenceService store,
        Game game, GameRuntimeState runtime)
    {
        if (runtime.IsTerminal)
        {
            game.FinishedAt = _timeProvider.GetUtcNow().UtcDateTime;
        }
        // Once accepted for processing, persist independently of a lost client connection.
        await store.SaveAsync(game, runtime, CancellationToken.None);
        _registry.PublishProgress(game.GameId, runtime, game.StateVersion);
    }

    private static string GetPlayerKey(GamePlayer player)
    {
        return player.UserId.HasValue
            ? GameSessionRules.CreateUserKey(player.UserId.Value)
            : GameSessionRules.CreateGuestKey(player.GuestIdentityId!.Value);
    }
}
