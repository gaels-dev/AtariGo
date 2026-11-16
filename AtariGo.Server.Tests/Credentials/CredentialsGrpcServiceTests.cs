using System.Net;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Grpc.Core;
using Grpc.Net.Client;

using AtariGo.Contracts;
using AtariGo.Server.Application.Authentication;
using AtariGo.Server.Application.Credentials;
using AtariGo.Server.Application.Sessions;
using AtariGo.Server.Data;
using AtariGo.Server.Data.Entities;
using AtariGo.Server.Security;
using AtariGo.Server.Services;
using AtariGo.Server.Tests.Games;

namespace AtariGo.Server.Tests.Credentials;

[TestClass]
[TestCategory("Integration")]
public sealed class CredentialsGrpcServiceTests
{
    private const string OriginalPassword = "OriginalSecret8";
    private const string NewPassword = " NewSecret9 ";
    private const int RequestTimeoutSeconds = 10;
    private const int DynamicPort = 0;

    [TestMethod]
    public async Task ChangePassword_AuthenticatedRequest_ChangesHashAndPreservesSession()
    {
        // Arrange
        await using SqlServerTestDatabase database = await SqlServerTestDatabase.CreateAsync();
        await SeedAsync(database);
        await using WebApplication app = await StartServerAsync(database.ConnectionString);
        using GrpcChannel channel = GrpcChannel.ForAddress(app.Urls.Single());
        var client = new Contracts.Credentials.CredentialsClient(channel);
        await using AtariGoDbContext context = database.CreateContext();
        string otherHash = await context.Credentials.Where(credential => credential.UserId == 2)
            .Select(credential => credential.PasswordHash).SingleAsync();
        // Act
        ChangePasswordResponse response = await SendAsync(client, CreateRequest(), "owner");
        using IServiceScope scope = app.Services.CreateScope();
        IAuthenticationService authentication = scope.ServiceProvider
            .GetRequiredService<IAuthenticationService>();
        LoginOperationResult oldLogin = await authentication.LoginAsync("Owner", OriginalPassword,
            CancellationToken.None);
        LoginOperationResult newLogin = await authentication.LoginAsync("Owner", NewPassword,
            CancellationToken.None);
        SessionIdentity? session = await scope.ServiceProvider.GetRequiredService<ISessionService>()
            .ValidateSessionAsync("owner", CancellationToken.None);
        Credential saved = await context.Credentials.AsNoTracking()
            .SingleAsync(credential => credential.UserId == 1);
        // Assert
        Assert.AreEqual(PasswordChangeStatus.Changed, response.Status);
        Assert.AreEqual(LoginStatus.IncorrectPassword, oldLogin.Status);
        Assert.AreEqual(LoginStatus.Success, newLogin.Status);
        Assert.IsNotNull(session);
        Assert.IsNotNull(saved.PasswordChangedAt);
        Assert.AreNotEqual(NewPassword, saved.PasswordHash);
        Assert.AreEqual(otherHash, await context.Credentials
            .Where(credential => credential.UserId == 2)
            .Select(credential => credential.PasswordHash).SingleAsync());
    }

    [TestMethod]
    public async Task ChangePassword_InvalidRequests_LeavesCredentialUntouched()
    {
        // Arrange
        await using SqlServerTestDatabase database = await SqlServerTestDatabase.CreateAsync();
        await SeedAsync(database);
        await using WebApplication app = await StartServerAsync(database.ConnectionString);
        using GrpcChannel channel = GrpcChannel.ForAddress(app.Urls.Single());
        var client = new Contracts.Credentials.CredentialsClient(channel);
        await using AtariGoDbContext context = database.CreateContext();
        string originalHash = await context.Credentials.Where(credential => credential.UserId == 1)
            .Select(credential => credential.PasswordHash).SingleAsync();
        var invalidCases = new[]
        {
            ("Wrong", NewPassword, NewPassword, PasswordChangeStatus.IncorrectCurrentPassword),
            (OriginalPassword, "short", "short", PasswordChangeStatus.InvalidNewPassword),
            (OriginalPassword, "lowercase9", "lowercase9",
                PasswordChangeStatus.InvalidNewPassword),
            (OriginalPassword, new string('A', 256), "Other",
                PasswordChangeStatus.InvalidNewPassword),
            (OriginalPassword, NewPassword, "Other", PasswordChangeStatus.ConfirmationMismatch)
        };
        // Act
        foreach ((string current, string password, string confirmation,
            PasswordChangeStatus expected) in invalidCases)
        {
            var request = new ChangePasswordRequest
            {
                CurrentPassword = current, NewPassword = password, Confirmation = confirmation
            };
            Assert.AreEqual(expected, (await SendAsync(client, request, "owner")).Status);
        }
        RpcException expired = await Assert.ThrowsAsync<RpcException>(
            () => SendAsync(client, CreateRequest(), "expired"));
        RpcException guest = await Assert.ThrowsAsync<RpcException>(
            () => client.ChangePasswordAsync(CreateRequest(), headers: new Metadata
            {
                { "x-guest-credential", "guest" }
            }, deadline: DateTime.UtcNow.AddSeconds(RequestTimeoutSeconds)).ResponseAsync);
        // Assert
        Assert.AreEqual(StatusCode.Unauthenticated, expired.StatusCode);
        Assert.AreEqual(StatusCode.Unauthenticated, guest.StatusCode);
        Assert.AreEqual(originalHash, await context.Credentials
            .Where(credential => credential.UserId == 1)
            .Select(credential => credential.PasswordHash).SingleAsync());
    }

    [TestMethod]
    public async Task ChangePassword_ConflictAndWriteFailure_PreservesConfirmedCredential()
    {
        // Arrange
        await using SqlServerTestDatabase database = await SqlServerTestDatabase.CreateAsync();
        await SeedAsync(database);
        var interceptor = new CredentialUpdateInterceptor();
        await using WebApplication app = await StartServerAsync(database.ConnectionString,
            interceptor);
        using GrpcChannel channel = GrpcChannel.ForAddress(app.Urls.Single());
        var client = new Contracts.Credentials.CredentialsClient(channel);
        await using AtariGoDbContext context = database.CreateContext();
        User owner = await context.Users.AsNoTracking().SingleAsync(user => user.UserId == 1);
        string concurrentPassword = "ConcurrentSecret8";
        string concurrentHash = new PasswordHasher<User>().HashPassword(owner, concurrentPassword);
        interceptor.BeforeUpdate = () => ReplaceHashAsync(database, concurrentHash);
        // Act
        ChangePasswordResponse conflict = await SendAsync(client, CreateRequest(), "owner");
        interceptor.FailUpdate = true;
        ChangePasswordRequest request = CreateRequest();
        request.CurrentPassword = concurrentPassword;
        RpcException failure = await Assert.ThrowsAsync<RpcException>(
            () => SendAsync(client, request, "owner"));
        // Assert
        Assert.AreEqual(PasswordChangeStatus.CredentialChanged, conflict.Status);
        Assert.AreEqual(StatusCode.Unavailable, failure.StatusCode);
        Assert.AreEqual("The password change was not confirmed.", failure.Status.Detail);
        Credential saved = await context.Credentials.AsNoTracking().SingleAsync(
            credential => credential.UserId == 1);
        Assert.AreEqual(concurrentHash, saved.PasswordHash);
        Assert.IsNull(saved.PasswordChangedAt);
    }

    private static Task<ChangePasswordResponse> SendAsync(
        Contracts.Credentials.CredentialsClient client, ChangePasswordRequest request, string token)
    {
        return client.ChangePasswordAsync(request,
            headers: new Metadata { { "authorization", "Bearer " + token } },
            deadline: DateTime.UtcNow.AddSeconds(RequestTimeoutSeconds)).ResponseAsync;
    }

    private static ChangePasswordRequest CreateRequest()
    {
        return new ChangePasswordRequest
        {
            CurrentPassword = OriginalPassword, NewPassword = NewPassword,
            Confirmation = NewPassword
        };
    }

    private static async Task ReplaceHashAsync(SqlServerTestDatabase database, string hash)
    {
        await using AtariGoDbContext context = database.CreateContext();
        await context.Credentials.Where(credential => credential.UserId == 1)
            .ExecuteUpdateAsync(setters => setters.SetProperty(
                credential => credential.PasswordHash, hash));
    }

    private static async Task<WebApplication> StartServerAsync(string connectionString,
        CredentialUpdateInterceptor? interceptor = null)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Configuration.Sources.Clear();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, DynamicPort,
            endpoint => endpoint.Protocols = HttpProtocols.Http2));
        builder.Services.AddGrpc();
        builder.Services.AddDbContext<AtariGoDbContext>(options =>
            ConfigureDatabase(options, connectionString, interceptor));
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<ISessionTokenProvider, SessionTokenProvider>();
        builder.Services.AddSingleton<ISessionConnectionRegistry, SessionConnectionRegistry>();
        builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        builder.Services.AddScoped<ICredentialsService, CredentialsService>();
        builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
        builder.Services.AddScoped<ISessionService, SessionService>();
        WebApplication app = builder.Build();
        app.MapGrpcService<CredentialsGrpcService>();
        await app.StartAsync();
        return app;
    }

    private static void ConfigureDatabase(DbContextOptionsBuilder options, string connectionString,
        CredentialUpdateInterceptor? interceptor)
    {
        options.UseSqlServer(connectionString);
        if (interceptor is not null)
        {
            options.AddInterceptors(interceptor);
        }
    }

    private static async Task SeedAsync(SqlServerTestDatabase database)
    {
        await using AtariGoDbContext context = database.CreateContext();
        int roleId = await context.Roles.Select(role => role.RoleId).SingleAsync();
        User owner = CreateUser("Owner", roleId);
        owner.Sessions.Add(new AtariGo.Server.Data.Entities.Session
        {
            SessionTokenHash = new SessionTokenProvider().Hash("owner"),
            State = "Active", StartedAt = DateTime.UtcNow, LastActivityAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        });
        context.Users.Add(owner);
        context.Users.Add(CreateUser("Other", roleId));
        await context.SaveChangesAsync();
    }

    private static User CreateUser(string name, int roleId)
    {
        var user = new User
        {
            UserName = name, Email = name + "@example.test", State = "Active", RoleId = roleId,
            RegistrationDate = DateTime.UtcNow
        };
        user.Credential = new Credential
        {
            PasswordHash = new PasswordHasher<User>().HashPassword(user, OriginalPassword)
        };
        return user;
    }
}
