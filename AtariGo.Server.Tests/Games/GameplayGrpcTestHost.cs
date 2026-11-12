using System.Net;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using AtariGo.GameLogic.Rules;
using AtariGo.Server.Application.Games;
using AtariGo.Server.Application.Matchmaking;
using AtariGo.Server.Application.Sessions;
using AtariGo.Server.Data;
using AtariGo.Server.Services;
using AtariGo.Server.Tests.Rooms;

namespace AtariGo.Server.Tests.Games;

internal sealed class GameplayGrpcTestHost : IAsyncDisposable
{
    private readonly WebApplication _app;

    private GameplayGrpcTestHost(WebApplication app, GameplayTestDatabase database)
    {
        _app = app;
        Database = database;
    }

    public GameplayTestDatabase Database { get; }
    public string Address => _app.Urls.Single();
    public IssuedGuestIdentity BlackIdentity { get; private set; } = null!;
    public IssuedGuestIdentity WhiteIdentity { get; private set; } = null!;

    public static async Task<GameplayGrpcTestHost> StartAsync()
    {
        GameplayTestDatabase database = await GameplayTestDatabase.CreateAsync();
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Configuration.Sources.Clear();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0,
            endpoint => endpoint.Protocols = HttpProtocols.Http2));
        builder.Services.AddGrpc();
        builder.Services.AddSingleton<TimeProvider>(database.Time);
        builder.Services.AddSingleton<IGameSessionRegistry, GameSessionRegistry>();
        builder.Services.AddSingleton<GameRulesEngine>();
        builder.Services.AddSingleton<GameCoordinator>();
        builder.Services.AddSingleton<IGuestIdentityRegistry, GuestIdentityRegistry>();
        builder.Services.AddScoped<GamePersistenceService>();
        builder.Services.AddScoped<GameActionProcessor>();
        builder.Services.AddScoped<IGameSessionCatalog, GameSessionCatalog>();
        builder.Services.AddScoped<ISessionService, TestRoomSessionService>();
        builder.Services.AddScoped(_ => database.CreateContext());
        WebApplication app = builder.Build();
        app.MapGrpcService<GameSessionGrpcService>();
        GameplayGrpcTestHost host = new GameplayGrpcTestHost(app, database);
        await host.AssignCredentialsAsync();
        await app.StartAsync();
        return host;
    }

    public async ValueTask DisposeAsync()
    {
        await _app.DisposeAsync();
        await Database.DisposeAsync();
    }

    private async Task AssignCredentialsAsync()
    {
        IGuestIdentityRegistry identities = _app.Services
            .GetRequiredService<IGuestIdentityRegistry>();
        BlackIdentity = identities.Create("Black player");
        WhiteIdentity = identities.Create("White player");
        await using AtariGoDbContext context = Database.CreateContext();
        foreach (AtariGo.Server.Data.Entities.GamePlayer player in context.GamePlayers)
        {
            player.GuestIdentityId = player.StoneColor == "Black"
                ? BlackIdentity.Identity.GuestId : WhiteIdentity.Identity.GuestId;
        }
        await context.SaveChangesAsync();
    }
}
