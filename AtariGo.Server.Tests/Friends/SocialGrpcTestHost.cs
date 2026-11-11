using System.Net;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using AtariGo.Server.Application.Friends;
using AtariGo.Server.Application.Friendships;
using AtariGo.Server.Application.RoomInvitations;
using AtariGo.Server.Application.Rooms;
using AtariGo.Server.Application.Sessions;
using AtariGo.Server.Application.Social;
using AtariGo.Server.Services;

namespace AtariGo.Server.Tests.Friends;

internal sealed class SocialGrpcTestHost : IAsyncDisposable
{
    private const int DynamicPort = 0;

    private readonly WebApplication _app;

    private SocialGrpcTestHost(WebApplication app, SocialTestDatabase database)
    {
        _app = app;
        Database = database;
    }

    public string Address => _app.Urls.Single();

    public SocialTestDatabase Database { get; }

    public static async Task<SocialGrpcTestHost> StartAsync()
    {
        SocialTestDatabase database = await SocialTestDatabase.CreateAsync();
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Configuration.Sources.Clear();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, DynamicPort,
            endpoint => endpoint.Protocols = HttpProtocols.Http2));
        builder.Services.AddGrpc();
        builder.Services.AddScoped(_ => database.CreateContext());
        builder.Services.AddScoped<ISessionService, SocialTestSessionService>();
        builder.Services.AddScoped<IFriendService, FriendService>();
        builder.Services.AddScoped<IFriendshipsService, FriendshipsService>();
        builder.Services.AddSingleton<ISessionConnectionRegistry, SessionConnectionRegistry>();
        builder.Services.AddSingleton<IRoomInvitationNotifier>(provider =>
            provider.GetRequiredService<ISessionConnectionRegistry>());
        builder.Services.AddSingleton<IRoomsService, RoomsService>();
        builder.Services.AddScoped<IRoomInvitationsService, RoomInvitationsService>();
        builder.Services.AddSingleton<FriendMutationCoordinator>();
        builder.Services.AddSingleton(TimeProvider.System);
        WebApplication app = builder.Build();
        app.MapGrpcService<FriendDirectoryGrpcService>();
        app.MapGrpcService<FriendshipsGrpcService>();
        app.MapGrpcService<SessionGrpcService>();
        app.MapGrpcService<RoomsGrpcService>();
        app.MapGrpcService<RoomInvitationsGrpcService>();
        await app.StartAsync();
        return new SocialGrpcTestHost(app, database);
    }

    public async ValueTask DisposeAsync()
    {
        await _app.DisposeAsync();
        await Database.DisposeAsync();
    }
}
