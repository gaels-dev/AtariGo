using System.Net;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Grpc.Core;
using Grpc.Net.Client;

using AtariGo.Contracts;
using AtariGo.Server.Application.GameHistory;
using AtariGo.Server.Application.Sessions;
using AtariGo.Server.Data;
using AtariGo.Server.Data.Entities;
using AtariGo.Server.Services;
using AtariGo.Server.Tests.Games;
using AtariGo.Server.Tests.Rooms;

namespace AtariGo.Server.Tests.GameHistory;

[TestClass]
[TestCategory("Integration")]
public sealed class GameHistoryGrpcServiceTests
{
    private const int DynamicPort = 0;
    private const int RequestTimeoutSeconds = 10;

    [TestMethod]
    public async Task ListGamesByPlayer_SqlServer_EnforcesOwnershipLimitAndReadOnlyAccess()
    {
        // Arrange
        await using SqlServerTestDatabase database = await SqlServerTestDatabase.CreateAsync();
        long[] expectedIds = await SeedAsync(database);
        await using WebApplication app = await StartServerAsync(database.ConnectionString);
        using GrpcChannel channel = GrpcChannel.ForAddress(app.Urls.Single());
        var client = new Contracts.GameHistory.GameHistoryClient(channel);
        await using AtariGoDbContext context = database.CreateContext();
        int playerCount = await context.GamePlayers.CountAsync();
        string[] results = await context.GamePlayers.OrderBy(player => player.GamePlayerId)
            .Select(player => player.Result).ToArrayAsync();
        // Act
        ListGamesByPlayerResponse history = await ReadAsync(client, "host");
        ListGamesByPlayerResponse empty = await ReadAsync(client, "other");
        RpcException unauthorized = await Assert.ThrowsAsync<RpcException>(
            () => ReadAsync(client, "expired"));
        RpcException guest = await Assert.ThrowsAsync<RpcException>(
            () => client.ListGamesByPlayerAsync(new ListGamesByPlayerRequest(),
                headers: new Metadata { { "x-guest-credential", "guest" } },
                deadline: DateTime.UtcNow.AddSeconds(RequestTimeoutSeconds)).ResponseAsync);
        // Assert
        CollectionAssert.AreEqual(expectedIds,
            history.Entries.Select(entry => entry.GameId).ToArray());
        Assert.HasCount(20, history.Entries);
        Assert.AreEqual("Guest-Rival", history.Entries[0].OpponentName);
        Assert.AreEqual("RegisteredRival", history.Entries[1].OpponentName);
        Assert.AreEqual("Black", history.Entries[0].StoneColor);
        Assert.AreEqual("White", history.Entries[0].OpponentStoneColor);
        Assert.AreEqual("Surrendered", history.Entries[0].Result);
        Assert.AreEqual(history.Entries[0].PlayedAtUnixMilliseconds,
            history.Entries[1].PlayedAtUnixMilliseconds);
        Assert.HasCount(0, empty.Entries);
        Assert.AreEqual(StatusCode.Unauthenticated, unauthorized.StatusCode);
        Assert.AreEqual(StatusCode.Unauthenticated, guest.StatusCode);
        Assert.AreEqual(playerCount, await context.GamePlayers.CountAsync());
        CollectionAssert.AreEqual(results, await context.GamePlayers
            .OrderBy(player => player.GamePlayerId).Select(player => player.Result).ToArrayAsync());
        Assert.AreEqual(0, context.ChangeTracker.Entries().Count());
    }

    [TestMethod]
    public async Task ListGamesByPlayer_IncompleteSummary_ReturnsErrorWithoutPartialHistory()
    {
        // Arrange
        await using SqlServerTestDatabase database = await SqlServerTestDatabase.CreateAsync();
        await SeedAsync(database);
        await using AtariGoDbContext context = database.CreateContext();
        GamePlayer rival = await context.GamePlayers
            .OrderByDescending(player => player.GamePlayerId)
            .FirstAsync(player => player.UserId == null
                && player.Game.GamePlayers.Any(owner => owner.UserId == 1));
        context.GamePlayers.Remove(rival);
        await context.SaveChangesAsync();
        await using WebApplication app = await StartServerAsync(database.ConnectionString);
        using GrpcChannel channel = GrpcChannel.ForAddress(app.Urls.Single());
        var client = new Contracts.GameHistory.GameHistoryClient(channel);
        // Act
        RpcException failure = await Assert.ThrowsAsync<RpcException>(
            () => ReadAsync(client, "host"));
        // Assert
        Assert.AreEqual(StatusCode.Internal, failure.StatusCode);
        Assert.AreEqual("The game history is unavailable.", failure.Status.Detail);
    }

    private static Task<ListGamesByPlayerResponse> ReadAsync(
        Contracts.GameHistory.GameHistoryClient client, string token)
    {
        return client.ListGamesByPlayerAsync(new ListGamesByPlayerRequest(),
            headers: new Metadata { { "authorization", "Bearer " + token } },
            deadline: DateTime.UtcNow.AddSeconds(RequestTimeoutSeconds)).ResponseAsync;
    }

    private static async Task<WebApplication> StartServerAsync(string connectionString)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Configuration.Sources.Clear();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, DynamicPort,
            endpoint => endpoint.Protocols = HttpProtocols.Http2));
        builder.Services.AddGrpc();
        builder.Services.AddDbContext<AtariGoDbContext>(options =>
            options.UseSqlServer(connectionString));
        builder.Services.AddScoped<ISessionService, TestRoomSessionService>();
        builder.Services.AddScoped<IGameHistoryService, GameHistoryService>();
        WebApplication app = builder.Build();
        app.MapGrpcService<GameHistoryGrpcService>();
        await app.StartAsync();
        return app;
    }

    private static async Task<long[]> SeedAsync(SqlServerTestDatabase database)
    {
        await using AtariGoDbContext context = database.CreateContext();
        int roleId = await context.Roles.Select(role => role.RoleId).SingleAsync();
        context.Users.Add(CreateUser("Owner", roleId));
        context.Users.Add(CreateUser("RegisteredRival", roleId));
        context.Users.Add(CreateUser("NoGames", roleId));
        await context.SaveChangesAsync();
        var games = new List<Game>();
        for (int index = 0; index < 22; index++)
        {
            games.Add(CreateGame(index, 1));
        }
        context.Games.AddRange(games);
        context.Games.Add(CreateGame(99, 2));
        await context.SaveChangesAsync();
        return games.OrderByDescending(game => game.StartedAt ?? game.GamePlayers.First().JoinedAt)
            .ThenByDescending(game => game.GameId).Take(20).Select(game => game.GameId).ToArray();
    }

    private static User CreateUser(string name, int roleId)
    {
        return new User
        {
            UserName = name,
            Email = name + "@example.test",
            State = "Active",
            RoleId = roleId,
            RegistrationDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
    }

    private static Game CreateGame(int index, int ownerId)
    {
        DateTime date = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(index / 2);
        bool isGuest = index % 2 != 0;
        var game = new Game
        {
            GameType = isGuest ? "Public" : "Private",
            State = "Completed",
            EndReason = "Surrender",
            StartedAt = index == 21 ? null : date,
            GameConfiguration = new GameConfiguration { CaptureGoal = 1, TurnTimeSeconds = 30 }
        };
        if (!isGuest)
        {
            game.Room = new Room
            {
                CreatorId = ownerId, InvitationCode = "H" + index.ToString("D5"),
                State = "Active", CreatedAt = date
            };
        }
        game.GamePlayers.Add(new GamePlayer
        {
            UserId = ownerId, StoneColor = "Black", TurnOrder = 1,
            JoinedAt = date, Result = "Surrendered"
        });
        game.GamePlayers.Add(new GamePlayer
        {
            UserId = isGuest ? null : 2,
            GuestIdentityId = isGuest ? Guid.NewGuid() : null,
            GuestName = isGuest ? "Guest-Rival" : null,
            StoneColor = "White", TurnOrder = 2, JoinedAt = date, Result = "Won"
        });
        return game;
    }
}
