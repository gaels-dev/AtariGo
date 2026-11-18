using System.Net;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Grpc.Net.Client;

using AtariGo.Contracts;
using AtariGo.Server.Application.Authentication;
using AtariGo.Server.Application.Email;
using AtariGo.Server.Application.Games;
using AtariGo.Server.Application.PasswordRecovery;
using AtariGo.Server.Application.Sessions;
using AtariGo.Server.Application.Social;
using AtariGo.Server.Data;
using AtariGo.Server.Data.Entities;
using AtariGo.Server.Security;
using AtariGo.Server.Services;
using AtariGo.Server.Tests.Games;
using AtariGo.Server.Tests.RoomInvitations;

namespace AtariGo.Server.Tests.PasswordRecovery;

internal sealed class RecoveryTestHost : IAsyncDisposable
{
    public const string OriginalPassword = "OriginalSecret8";
    public const string NewPassword = " NewSecret9 ";
    private const int RequestTimeoutSeconds = 30;
    private WebApplication? _application;
    private GrpcChannel? _channel;

    private RecoveryTestHost(SqlServerTestDatabase database)
    {
        Database = database;
    }

    public SqlServerTestDatabase Database
    {
        get;
    }

    public ManualTimeProvider Clock { get; } = new ManualTimeProvider();

    public RecordingEmailSender Email { get; } = new RecordingEmailSender();

    public IServiceProvider Services => _application!.Services;

    public GrpcChannel Channel => _channel!;

    public Contracts.PasswordRecovery.PasswordRecoveryClient Client =>
        new Contracts.PasswordRecovery.PasswordRecoveryClient(Channel);

    public static async Task<RecoveryTestHost> CreateAsync()
    {
        var host = new RecoveryTestHost(await SqlServerTestDatabase.CreateAsync());
        try
        {
            await host.SeedAsync();
            await host.StartAsync();
            return host;
        }
        catch
        {
            await host.DisposeAsync();
            throw;
        }
    }

    public Task<PasswordRecoveryResponse> RequestAsync(string identifier = "Owner")
    {
        return Client.RequestPasswordRecoveryAsync(
            new PasswordRecoveryRequest { Identifier = identifier },
            deadline: DateTime.UtcNow.AddSeconds(RequestTimeoutSeconds)).ResponseAsync;
    }

    public Task<PasswordRecoveryResponse> ResetAsync(ResetPasswordRequest request)
    {
        return Client.ResetPasswordAsync(request,
            deadline: DateTime.UtcNow.AddSeconds(RequestTimeoutSeconds)).ResponseAsync;
    }

    public ResetPasswordRequest CreateReset(string? code = null)
    {
        return new ResetPasswordRequest
        {
            Identifier = "Owner",
            Code = code ?? Email.Codes.Last(),
            NewPassword = NewPassword,
            Confirmation = NewPassword
        };
    }

    public async Task RestartAsync()
    {
        _channel?.Dispose();
        if (_application is not null)
        {
            await _application.DisposeAsync();
        }
        await StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        _channel?.Dispose();
        if (_application is not null)
        {
            await _application.DisposeAsync();
        }
        await Database.DisposeAsync();
    }

    private async Task StartAsync()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Configuration.Sources.Clear();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0,
            endpoint => endpoint.Protocols = HttpProtocols.Http2));
        builder.Services.AddGrpc();
        builder.Services.AddDbContext<AtariGoDbContext>(options => options
            .UseSqlServer(Database.ConnectionString));
        builder.Services.AddSingleton<TimeProvider>(Clock);
        builder.Services.AddSingleton<IEmailSender>(Email);
        builder.Services.Configure<PasswordRecoveryOptions>(options =>
        {
            options.HmacKey = new string('K', 64);
            options.IpRequestsPerHour = 100;
        });
        RegisterServices(builder.Services);
        _application = builder.Build();
        _application.MapGrpcService<PasswordRecoveryGrpcService>();
        _application.MapGrpcService<AuthenticationGrpcService>();
        _application.MapGrpcService<SessionGrpcService>();
        await _application.StartAsync();
        _channel = GrpcChannel.ForAddress(_application.Urls.Single());
    }

    private static void RegisterServices(IServiceCollection services)
    {
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddSingleton<RecoveryCodeProtector>();
        services.AddSingleton<RecoverySessionRevoker>();
        services.AddScoped<RecoveryCodeDelivery>();
        services.AddScoped<PasswordResetWriter>();
        services.AddScoped<IPasswordRecoveryService, PasswordRecoveryService>();
        services.AddScoped<ISessionService, SessionService>();
        services.AddLoginVerification();
        services.AddSingleton<ISessionTokenProvider, SessionTokenProvider>();
        services.AddSingleton<ISessionConnectionRegistry, SessionConnectionRegistry>();
        services.AddSingleton<IGameSessionRegistry, GameSessionRegistry>();
        services.AddSingleton<FriendMutationCoordinator>();
    }

    private async Task SeedAsync()
    {
        await using AtariGoDbContext context = Database.CreateContext();
        DateTime now = Clock.GetUtcNow().UtcDateTime;
        foreach (string name in new[] { "Owner", "Other" })
        {
            var user = new User
            {
                UserName = name,
                Email = name + "@example.test",
                State = "Active",
                RegistrationDate = now
            };
            user.Credential = new Credential
            {
                PasswordHash = new PasswordHasher<User>().HashPassword(user, OriginalPassword)
            };
            user.Sessions.Add(new AtariGo.Server.Data.Entities.Session
            {
                SessionTokenHash = new SessionTokenProvider().Hash(name),
                State = "Active",
                StartedAt = now,
                LastActivityAt = now,
                ExpiresAt = now.AddHours(1)
            });
            context.Users.Add(user);
        }
        await context.SaveChangesAsync();
    }
}
