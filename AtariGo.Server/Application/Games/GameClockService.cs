using Microsoft.EntityFrameworkCore;

using AtariGo.Server.Data;

namespace AtariGo.Server.Application.Games;

public sealed class GameClockService : BackgroundService
{
    private const int PollMilliseconds = 1000;
    private const int BatchSize = 100;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly GameCoordinator _coordinator;
    private readonly IGameSessionRegistry _registry;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<GameClockService> _logger;

    public GameClockService(IServiceScopeFactory scopeFactory, GameCoordinator coordinator,
        IGameSessionRegistry registry, TimeProvider timeProvider, ILogger<GameClockService> logger)
    {
        _scopeFactory = scopeFactory;
        _coordinator = coordinator;
        _registry = registry;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    private async Task AdvanceDueGamesAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        AtariGoDbContext database = scope.ServiceProvider.GetRequiredService<AtariGoDbContext>();
        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        List<long> due = await database.Games.AsNoTracking()
            .Where(game => (game.State == GameRuntimeState.Waiting
                || game.State == GameRuntimeState.Playing) && game.TurnDeadlineUtc <= now)
            .OrderBy(game => game.TurnDeadlineUtc).Select(game => game.GameId)
            .Take(BatchSize).ToListAsync(cancellationToken);
        foreach (long gameId in due)
        {
            await _coordinator.AdvanceAsync(gameId, cancellationToken);
        }
        _registry.PruneIdleSessions();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new PeriodicTimer(
            TimeSpan.FromMilliseconds(PollMilliseconds), _timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await AdvanceDueGamesAsync(stoppingToken);
                }
                catch (DbUpdateConcurrencyException exception)
                {
                    _logger.LogWarning(exception, "A concurrent game transition will be reloaded.");
                }
                catch (System.Data.Common.DbException exception)
                {
                    _logger.LogError(exception, "Game clocks cannot access the database.");
                }
                catch (DbUpdateException exception)
                {
                    _logger.LogError(exception, "A game clock transition could not be persisted.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Game clock supervision stopped.");
        }
    }
}
