using System;
using System.Linq;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using AtariGo.Server.Application.Authentication;
using AtariGo.Server.Application.Friends;
using AtariGo.Server.Application.Friendships;
using AtariGo.Server.Application.Games;
using AtariGo.Server.Application.Leaderboard;
using AtariGo.Server.Application.Matchmaking;
using AtariGo.Server.Application.RoomInvitations;
using AtariGo.Server.Application.Rooms;
using AtariGo.Server.Application.Sessions;
using AtariGo.Server.Configuration;
using AtariGo.Server.Data;
using AtariGo.Server.Data.Entities;
using AtariGo.Server.Security;
using AtariGo.Server.Services;

string[] hostArguments = args
    .Where(argument => argument != DatabaseReadinessCommand.CommandLineSwitch)
    .ToArray();
WebApplicationBuilder builder = WebApplication.CreateBuilder(hostArguments);

string? connectionString = builder.Configuration.GetConnectionString(
    DatabaseConnectionSettings.ConnectionName);

builder.Services.AddDbContext<AtariGoDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<DatabaseReadinessCheck>();

builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<IFriendService, FriendService>();
builder.Services.AddScoped<IFriendshipsService, FriendshipsService>();
builder.Services.AddSingleton<FriendMutationCoordinator>();
builder.Services.AddScoped<IRegistrationService, RegistrationService>();
builder.Services.AddScoped<ILeaderboardService, LeaderboardService>();
builder.Services.AddScoped<ISessionService, SessionService>();
builder.Services.AddScoped<IGameLifecycleService, GameLifecycleService>();
builder.Services.AddScoped<IGameSessionCatalog, GameSessionCatalog>();
builder.Services.AddSingleton<IGameSessionRegistry, GameSessionRegistry>();
builder.Services.AddSingleton<IGuestIdentityRegistry, GuestIdentityRegistry>();
builder.Services.AddSingleton<InMemoryPublicMatchmakingQueue>();
builder.Services.AddSingleton<IRoomInvitationNotifier>(provider =>
    provider.GetRequiredService<ISessionConnectionRegistry>());
builder.Services.AddSingleton<IRoomsService, RoomsService>();
builder.Services.AddScoped<IRoomInvitationsService, RoomInvitationsService>();
builder.Services.AddHostedService<RoomInvitationExpiryService>();
builder.Services.AddSingleton<ISessionConnectionRegistry, SessionConnectionRegistry>();
builder.Services.AddSingleton<RegistrationValidator>();
builder.Services.AddSingleton<ISessionTokenProvider, SessionTokenProvider>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

builder.Services.AddGrpc();

await using WebApplication app = builder.Build();

if (!DatabaseConnectionSettings.Validate(app.Configuration, app.Logger))
{
    Environment.ExitCode = DatabaseReadinessCommand.FailureExitCode;
    return;
}

app.MapGrpcService<AuthenticationGrpcService>();
app.MapGrpcService<FriendDirectoryGrpcService>();
app.MapGrpcService<FriendshipsGrpcService>();
app.MapGrpcService<RegistrationGrpcService>();
app.MapGrpcService<LeaderboardGrpcService>();
app.MapGrpcService<SessionGrpcService>();
app.MapGrpcService<RoomsGrpcService>();
app.MapGrpcService<RoomInvitationsGrpcService>();
app.MapGrpcService<GameLifecycleGrpcService>();
app.MapGrpcService<GameSessionGrpcService>();
app.MapGrpcService<MatchmakingGrpcService>();
app.MapGet("/", () =>
    "Communication with gRPC endpoints must be made through a gRPC client.");

if (args.Contains(DatabaseReadinessCommand.CommandLineSwitch, StringComparer.Ordinal))
{
    Environment.ExitCode = await DatabaseReadinessCommand.RunAsync(app.Services, app.Logger);
}
else
{
    await app.RunAsync();
}
