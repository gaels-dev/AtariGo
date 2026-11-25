using System.Net;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Grpc.Net.Client;

using AtariGo.Contracts;
using AtariGo.Diagnostics;
using AtariGo.Server.Application.Leaderboard;
using AtariGo.Server.Data;
using AtariGo.Server.Services;
using AtariGo.Server.Tests.Games;

namespace AtariGo.Server.Tests.Diagnostics;

[TestClass]
[TestCategory("Integration")]
public sealed class LeaderboardGrpcActivationTests
{
    private const int DynamicPort = 0;
    private const int RequestTimeoutSeconds = 10;
    private readonly TestContext _testContext;

    public LeaderboardGrpcActivationTests(TestContext testContext)
    {
        _testContext = testContext;
    }

    [TestMethod]
    public async Task GetTopPlayers_GrpcActivation_QueriesDatabaseAndRecordsOutcomeAsync()
    {
        // Arrange
        await using GameplayTestDatabase database = await GameplayTestDatabase.CreateAsync();
        using var records = new RecordingLogProvider();
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Configuration.Sources.Clear();
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(records);
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, DynamicPort,
            endpoint => endpoint.Protocols = HttpProtocols.Http2));
        builder.Services.AddGrpc();
        builder.Services.AddScoped<AtariGoDbContext>(_ => database.CreateContext());
        builder.Services.AddScoped<ILeaderboardService, LeaderboardService>();
        await using WebApplication app = builder.Build();
        app.MapGrpcService<LeaderboardGrpcService>();
        await app.StartAsync(_testContext.CancellationToken);
        using GrpcChannel channel = GrpcChannel.ForAddress(app.Urls.Single());
        var client = new Leaderboard.LeaderboardClient(channel);
        // Act
        GetTopPlayersResponse response = await client.GetTopPlayersAsync(
            new GetTopPlayersRequest { Limit = 1 },
            deadline: DateTime.UtcNow.AddSeconds(RequestTimeoutSeconds),
            cancellationToken: _testContext.CancellationToken);
        // Assert
        Assert.IsEmpty(response.Entries);
        DiagnosticEvent entry = records.States.OfType<DiagnosticEvent>().Single();
        Assert.AreEqual("QueryLeaderboard", entry.Definition.Operation);
        Assert.AreEqual(DiagnosticOutcome.Succeeded, entry.Details.Outcome);
    }

    [TestMethod]
    public void CreateFactory_AllMappedGrpcServices_HaveUnambiguousConstructors()
    {
        // Arrange
        Type[] services = typeof(LeaderboardGrpcService).Assembly.GetTypes()
            .Where(type => type.Namespace == typeof(LeaderboardGrpcService).Namespace
                && type.Name.EndsWith("GrpcService", StringComparison.Ordinal)).ToArray();
        // Act
        ObjectFactory[] factories = services.Select(type =>
            ActivatorUtilities.CreateFactory(type, Type.EmptyTypes)).ToArray();
        // Assert
        Assert.HasCount(16, factories);
    }
}
