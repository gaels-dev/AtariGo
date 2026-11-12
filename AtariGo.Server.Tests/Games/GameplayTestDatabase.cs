using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

using AtariGo.GameLogic.Models;
using AtariGo.GameLogic.Rules;
using AtariGo.Server.Application.Games;
using AtariGo.Server.Data;
using AtariGo.Server.Data.Entities;
using AtariGo.Server.Tests.RoomInvitations;

namespace AtariGo.Server.Tests.Games;

internal sealed class GameplayTestDatabase : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    private GameplayTestDatabase(SqliteConnection connection, ServiceProvider services)
    {
        _connection = connection;
        Services = services;
    }

    public ServiceProvider Services { get; }
    public ManualTimeProvider Time => Services.GetRequiredService<ManualTimeProvider>();
    public GameCoordinator Coordinator => Services.GetRequiredService<GameCoordinator>();
    public IGameSessionRegistry Registry => Services.GetRequiredService<IGameSessionRegistry>();
    public long GameId { get; private set; }
    public Guid BlackGuestId { get; } = Guid.NewGuid();
    public Guid WhiteGuestId { get; } = Guid.NewGuid();

    public static async Task<GameplayTestDatabase> CreateAsync(
        DbCommandInterceptor? interceptor = null)
    {
        SqliteConnection connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        connection.CreateCollation("Latin1_General_100_CI_AI_SC",
            StringComparer.OrdinalIgnoreCase.Compare);
        connection.CreateFunction("sysutcdatetime", () => DateTime.UtcNow.ToString("O"));
        ServiceCollection services = new ServiceCollection();
        DbContextOptionsBuilder<AtariGoDbContext> options =
            new DbContextOptionsBuilder<AtariGoDbContext>().UseSqlite(connection);
        if (interceptor is not null)
        {
            options.AddInterceptors(interceptor);
        }
        services.AddScoped<AtariGoDbContext>(_ => new GameplayTestDbContext(options.Options));
        services.AddSingleton<ManualTimeProvider>();
        services.AddSingleton<TimeProvider>(
            provider => provider.GetRequiredService<ManualTimeProvider>());
        services.AddSingleton<IGameSessionRegistry, GameSessionRegistry>();
        services.AddSingleton<GameRulesEngine>();
        services.AddSingleton<GameCoordinator>();
        services.AddScoped<GameActionProcessor>();
        services.AddScoped<GamePersistenceService>();
        GameplayTestDatabase database = new GameplayTestDatabase(connection,
            services.BuildServiceProvider());
        await database.SeedAsync();
        return database;
    }

    public Task<GameSessionConnection> ConnectAsync(string color)
    {
        bool black = color == "Black";
        GameSessionParticipant blackPlayer = new GameSessionParticipant(
            GameSessionRules.CreateGuestKey(BlackGuestId), "Black player", "Black");
        GameSessionParticipant whitePlayer = new GameSessionParticipant(
            GameSessionRules.CreateGuestKey(WhiteGuestId), "White player", "White");
        return Coordinator.ConnectAsync(new GameSessionDefinition
        {
            GameId = GameId,
            CaptureGoal = 1,
            TurnTimeSeconds = 30,
            Player = black ? blackPlayer : whitePlayer,
            Opponent = black ? whitePlayer : blackPlayer
        }, CancellationToken.None);
    }

    public async Task<Game> ReadAsync()
    {
        using IServiceScope scope = Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<GamePersistenceService>()
            .LoadAsync(GameId, CancellationToken.None);
    }

    public async Task<GameActionCommand> CreateMoveAsync(BoardPosition position)
    {
        Game progress = await ReadAsync();
        return new GameActionCommand
        {
            ActionId = Guid.NewGuid().ToString("N"),
            ExpectedVersion = progress.StateVersion,
            Row = position.Row,
            Column = position.Column
        };
    }

    public AtariGoDbContext CreateContext()
    {
        DbContextOptions<AtariGoDbContext> options = new DbContextOptionsBuilder<AtariGoDbContext>()
            .UseSqlite(_connection).Options;
        return new GameplayTestDbContext(options);
    }

    public async ValueTask DisposeAsync()
    {
        await Services.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private async Task SeedAsync()
    {
        using IServiceScope scope = Services.CreateScope();
        AtariGoDbContext context = scope.ServiceProvider.GetRequiredService<AtariGoDbContext>();
        await context.Database.EnsureCreatedAsync();
        DateTime now = Time.GetUtcNow().UtcDateTime;
        Game game = new Game
        {
            GameType = "Public",
            State = "InProgress",
            StartedAt = now,
            GameConfiguration = new GameConfiguration { CaptureGoal = 1, TurnTimeSeconds = 30 }
        };
        game.GamePlayers.Add(new GamePlayer
        {
            GuestIdentityId = BlackGuestId,
            GuestName = "Black player",
            StoneColor = "Black",
            TurnOrder = 1,
            JoinedAt = now,
            Result = "Pending"
        });
        game.GamePlayers.Add(new GamePlayer
        {
            GuestIdentityId = WhiteGuestId,
            GuestName = "White player",
            StoneColor = "White",
            TurnOrder = 2,
            JoinedAt = now,
            Result = "Pending"
        });
        GamePersistenceService.Initialize(game, 1, now);
        context.Games.Add(game);
        await context.SaveChangesAsync();
        GameId = game.GameId;
    }
}
