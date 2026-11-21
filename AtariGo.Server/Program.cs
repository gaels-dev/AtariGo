using System.Security.Cryptography.X509Certificates;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using AtariGo.GameLogic.Rules;
using AtariGo.Server.Application.Authentication;
using AtariGo.Server.Application.Credentials;
using AtariGo.Server.Application.Email;
using AtariGo.Server.Application.Friends;
using AtariGo.Server.Application.Friendships;
using AtariGo.Server.Application.GameHistory;
using AtariGo.Server.Application.Games;
using AtariGo.Server.Application.Leaderboard;
using AtariGo.Server.Application.Matchmaking;
using AtariGo.Server.Application.PasswordRecovery;
using AtariGo.Server.Application.Profile;
using AtariGo.Server.Application.Reports;
using AtariGo.Server.Application.RoomInvitations;
using AtariGo.Server.Application.Rooms;
using AtariGo.Server.Application.Sessions;
using AtariGo.Server.Application.Social;
using AtariGo.Server.Configuration;
using AtariGo.Server.Data;
using AtariGo.Server.Data.Entities;
using AtariGo.Server.Security;
using AtariGo.Server.Services;

const int ConfigurationFailureExitCode = 1;
const int ConnectionProbeDelaySeconds = 10;
const int ConnectionProbeTimeoutSeconds = 5;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.Http2.KeepAlivePingDelay =
        TimeSpan.FromSeconds(ConnectionProbeDelaySeconds);
    options.Limits.Http2.KeepAlivePingTimeout =
        TimeSpan.FromSeconds(ConnectionProbeTimeoutSeconds);
});
X509Certificate2? lanCertificate = LanEndpointSettings.Configure(builder);

string? connectionString = builder.Configuration.GetConnectionString(
    DatabaseConnectionSettings.ConnectionName);

builder.Services.AddDbContext<AtariGoDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddLoginVerification();
builder.Services.AddScoped<ICredentialsService, CredentialsService>();
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
builder.Services.AddOptions<PasswordRecoveryOptions>()
    .Bind(builder.Configuration.GetSection("PasswordRecovery"))
    .Validate(options => options.AccountRequestsPerHour > 0 && options.IpRequestsPerHour > 0
        && options.ResendSeconds >= PasswordRecoveryOptions.MinimumResendSeconds);
builder.Services.AddSingleton<RecoveryCodeProtector>();
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<RecoveryCodeDelivery>();
builder.Services.AddScoped<PasswordResetWriter>();
builder.Services.AddSingleton<RecoverySessionRevoker>();
builder.Services.AddScoped<IPasswordRecoveryService, PasswordRecoveryService>();
builder.Services.AddScoped<IFriendService, FriendService>();
builder.Services.AddScoped<IFriendshipsService, FriendshipsService>();
builder.Services.AddSingleton<FriendMutationCoordinator>();
builder.Services.AddScoped<IRegistrationService, RegistrationService>();
builder.Services.AddScoped<ILeaderboardService, LeaderboardService>();
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IAccountSanctionService, AccountSanctionService>();
builder.Services.AddScoped<IGameHistoryService, GameHistoryService>();
builder.Services.AddScoped<ISessionService, SessionService>();
builder.Services.AddScoped<IGameLifecycleService, GameLifecycleService>();
builder.Services.AddSingleton<GameRulesEngine>();
builder.Services.AddScoped<GamePersistenceService>();
builder.Services.AddScoped<GameActionProcessor>();
builder.Services.AddSingleton<GameOperationGate>();
builder.Services.AddSingleton<GameCoordinator>();
builder.Services.AddScoped<GameCommandService>();
builder.Services.AddHostedService<GameClockService>();
builder.Services.AddScoped<IGameSessionCatalog, GameSessionCatalog>();
builder.Services.AddSingleton<IGameSessionRegistry, GameSessionRegistry>();
builder.Services.AddSingleton<IGuestIdentityRegistry, GuestIdentityRegistry>();
builder.Services.AddSingleton<InMemoryPublicMatchmakingQueue>();
builder.Services.AddScoped<PublicMatchmakingService>();
builder.Services.AddSingleton<IRoomInvitationNotifier>(provider =>
    provider.GetRequiredService<ISessionConnectionRegistry>());
builder.Services.AddSingleton<IRoomsService, RoomsService>();
builder.Services.AddScoped<IRoomInvitationsService, RoomInvitationsService>();
builder.Services.AddSingleton<RoomInvitationEmailLimiter>();
builder.Services.AddScoped<RoomInvitationEmailDeliveryService>();
builder.Services.AddHostedService<RoomStateExpiryService>();
builder.Services.AddSingleton<ISessionConnectionRegistry, SessionConnectionRegistry>();
builder.Services.AddSingleton<RegistrationValidator>();
builder.Services.AddSingleton<ISessionTokenProvider, SessionTokenProvider>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

builder.Services.AddGrpc();

await using WebApplication app = builder.Build();
if (lanCertificate is not null)
{
    app.Lifetime.ApplicationStopped.Register(lanCertificate.Dispose);
}

if (!DatabaseConnectionSettings.Validate(app.Configuration, app.Logger))
{
    lanCertificate?.Dispose();
    Environment.ExitCode = ConfigurationFailureExitCode;
    return;
}

app.MapGrpcService<AuthenticationGrpcService>();
app.MapGrpcService<CredentialsGrpcService>();
app.MapGrpcService<PasswordRecoveryGrpcService>();
app.MapGrpcService<FriendDirectoryGrpcService>();
app.MapGrpcService<FriendshipsGrpcService>();
app.MapGrpcService<RegistrationGrpcService>();
app.MapGrpcService<LeaderboardGrpcService>();
app.MapGrpcService<ProfileGrpcService>();
app.MapGrpcService<ReportsGrpcService>();
app.MapGrpcService<GameHistoryGrpcService>();
app.MapGrpcService<SessionGrpcService>();
app.MapGrpcService<RoomsGrpcService>();
app.MapGrpcService<RoomInvitationsGrpcService>();
app.MapGrpcService<GameLifecycleGrpcService>();
app.MapGrpcService<GameSessionGrpcService>();
app.MapGrpcService<MatchmakingGrpcService>();
app.MapGet("/", () =>
    "Communication with gRPC endpoints must be made through a gRPC client.");

await app.RunAsync();
