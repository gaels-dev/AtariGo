using Microsoft.EntityFrameworkCore;

namespace AtariGo.Server.Application.Games;

public sealed class GameClockService : BackgroundService
{
    private const int PollMilliseconds = 1000;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly GameCoordinator _coordinator;
    private readonly IGameSessionRegistry _registry;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<GameClockService> _logger;

    public GameClockService(IServiceScopeFactory scopeFactory, GameCoordinator coordinator,
        IGameSessionRegistry registry, TimeProvider timeProvider, ILogger<GameClockService> logger)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(coordinator);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        _scopeFactory = scopeFactory;
        _coordinator = coordinator;
        _registry = registry;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    private async Task AdvanceDueGamesAsync(CancellationToken cancellationToken)
    {
        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        IReadOnlyList<long> due = _registry.GetDueGames(now);
        foreach (long gameId in due)
        {
            await AdvanceGameAsync(gameId, cancellationToken);
        }
        _registry.PruneIdleSessions();
    }

    private async Task AdvanceGameAsync(long gameId, CancellationToken cancellationToken)
    {
        try
        {
            await _coordinator.AdvanceAsync(gameId, cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            _logger.LogWarning(exception, "A concurrent game transition will be reloaded.");
        }
        catch (System.Data.Common.DbException exception)
        {
            _logger.LogError(exception, "A game clock cannot access the database.");
        }
        catch (DbUpdateException exception)
        {
            _logger.LogError(exception, "A game clock transition could not be persisted.");
        }
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        GamePersistenceService store = scope.ServiceProvider
            .GetRequiredService<GamePersistenceService>();
        await store.CloseInterruptedGamesAsync(cancellationToken);
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new PeriodicTimer(
            TimeSpan.FromMilliseconds(PollMilliseconds), _timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await AdvanceDueGamesAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Game clock supervision stopped.");
        }
    }
}
