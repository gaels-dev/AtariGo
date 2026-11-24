using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Grpc.Core;

using AtariGo.Contracts;
using AtariGo.Server.Application.GameHistory;
using AtariGo.Server.Application.Leaderboard;
using AtariGo.Server.Application.Profile;
using AtariGo.Server.Application.Reports;
using AtariGo.Server.Application.Sessions;
using AtariGo.Server.Data;
using AtariGo.Server.Data.Entities;
using AtariGo.Server.Services;
using AtariGo.Server.Tests.Games;
using AtariGo.Server.Tests.Rooms;

namespace AtariGo.Server.Tests.Diagnostics;

[TestClass]
[TestCategory("Integration")]
public sealed class PlayerLoggingTests
{
    private readonly TestContext _testContext;

    public PlayerLoggingTests(TestContext testContext)
    {
        _testContext = testContext;
    }

    [TestMethod]
    public async Task ProfileAndQueries_ReturnPrivateDataWithoutLoggingPayloadsAsync()
    {
        // Arrange
        await using var logs = new ServerLogTestHost(fileLevel: "Information");
        await using GameplayTestDatabase database = await GameplayTestDatabase.CreateAsync();
        using AtariGoDbContext context = database.CreateContext();
        context.Users.Add(new User
        {
            UserName = "privateName",
            Email = "private@example.invalid",
            State = "Active",
            RegistrationDate = DateTime.UtcNow
        });
        await context.SaveChangesAsync(_testContext.CancellationToken);
        var sessions = new TestRoomSessionService();
        var profile = new ProfileGrpcService(new ProfileService(context, database.Time,
            logs.Application.Services.GetRequiredService<ILogger<ProfileService>>()), sessions,
            new SessionConnectionRegistry(),
            logs.Application.Services.GetRequiredService<ILogger<ProfileGrpcService>>());
        var call = new GameSessionTestCallContext("private-token");
        call.RequestHeaders.Add("authorization", "Bearer host");
        // Act
        GetProfileResponse read = await profile.GetProfile(new GetProfileRequest(), call);
        UpdateProfileResponse update = await profile.UpdateProfile(new UpdateProfileRequest
        {
            UserName = "privateNew",
            AvatarId = 1
        }, call);
        var history = new GameHistoryGrpcService(new GameHistoryService(context), sessions,
            logs.Application.Services.GetRequiredService<ILogger<GameHistoryGrpcService>>());
        ListGamesByPlayerResponse games = await history.ListGamesByPlayer(
            new ListGamesByPlayerRequest(), call);
        var leaderboard = new LeaderboardGrpcService(new LeaderboardService(context),
            logs.Application.Services.GetRequiredService<ILogger<LeaderboardGrpcService>>());
        GetTopPlayersResponse top = await leaderboard.GetTopPlayers(
            new GetTopPlayersRequest(), call);
        List<JsonElement> entries = await logs.ReadEventsAsync(_testContext.CancellationToken);
        // Assert
        Assert.AreEqual("privateName", read.Profile.UserName);
        Assert.AreEqual(Contracts.ProfileUpdateStatus.Updated, update.Status);
        Assert.IsEmpty(games.Entries);
        Assert.IsEmpty(top.Entries);
        Assert.HasCount(5, entries);
        Assert.IsTrue(entries.All(entry =>
            entry.GetProperty("Outcome").GetString() == "Succeeded"));
        Assert.DoesNotContain(message => message.Contains("private", StringComparison.Ordinal),
            logs.Records.Messages);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var interrupted = new GameSessionTestCallContext("private-token", cancelled.Token);
        interrupted.RequestHeaders.Add("authorization", "Bearer host");
        var valid = new UpdateProfileRequest { UserName = "privateNext", AvatarId = 1 };
        await Assert.ThrowsAsync<RpcException>(() =>
            profile.GetProfile(new GetProfileRequest(), interrupted));
        await Assert.ThrowsAsync<RpcException>(() => profile.UpdateProfile(valid, interrupted));
        await Assert.ThrowsAsync<RpcException>(() =>
            history.ListGamesByPlayer(new ListGamesByPlayerRequest(), interrupted));
        await context.Database.GetDbConnection().CloseAsync();
        await Assert.ThrowsAsync<RpcException>(() =>
            profile.GetProfile(new GetProfileRequest(), call));
        await Assert.ThrowsAsync<RpcException>(() => profile.UpdateProfile(valid, call));
        await Assert.ThrowsAsync<RpcException>(() =>
            history.ListGamesByPlayer(new ListGamesByPlayerRequest(), call));
        await context.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            profile.GetProfile(new GetProfileRequest(), call));
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            profile.UpdateProfile(new UpdateProfileRequest
            {
                UserName = "privateNext",
                AvatarId = 1
            }, call));
        await Assert.ThrowsAsync<RpcException>(() =>
            history.ListGamesByPlayer(new ListGamesByPlayerRequest(), call));
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            leaderboard.GetTopPlayers(new GetTopPlayersRequest(), call));
    }

    [TestMethod]
    [DataRow("Chat", "ChatMuteApplied")]
    [DataRow("Inactivity", "SuspensionApplied")]
    public async Task Reports_ThresholdAndDuplicate_LogOnlyCommittedChangesAsync(string category,
        string expectedOperation)
    {
        // Arrange
        await using var logs = new ServerLogTestHost(fileLevel: "Information");
        await using GameplayTestDatabase database = await GameplayTestDatabase.CreateAsync();
        using AtariGoDbContext context = database.CreateContext();
        IReadOnlyList<Game> games = await SeedReportsAsync(context);
        var reports = new ReportService(context, database.Time,
            logs.Application.Services.GetRequiredService<ILogger<ReportService>>());
        var transport = new ReportsGrpcService(reports, new TestRoomSessionService(),
            logs.Application.Services.GetRequiredService<ILogger<ReportsGrpcService>>());
        var call = new GameSessionTestCallContext("private-token");
        call.RequestHeaders.Add("authorization", "Bearer host");
        var request = new SubmitReportRequest
        {
            GameId = games[0].GameId,
            Description = "private-description",
            Category = category == "Chat" ? ReportCategory.Chat : ReportCategory.Inactivity
        };
        // Act
        await transport.SubmitReport(request, call);
        foreach (Game game in games.Skip(1))
        {
            await reports.SubmitReportAsync(
                new SessionIdentity(game.GamePlayers.First().UserId!.Value,
                "private-name"),
                new SubmitReportCommand(game.GameId, category, "private-description"),
                _testContext.CancellationToken);
        }
        await transport.SubmitReport(request, call);
        List<JsonElement> entries = await logs.ReadEventsAsync(_testContext.CancellationToken);
        Sanction sanction = await context.Sanctions.SingleAsync(_testContext.CancellationToken);
        // Assert
        Assert.HasCount(3, entries.Where(entry => ReadOperation(entry) == "ReportCommitted"));
        JsonElement applied = entries.Single(entry => ReadOperation(entry) == expectedOperation);
        Assert.AreEqual(sanction.TriggeringReportId, applied.GetProperty("ReportId").GetInt64());
        Assert.AreEqual(sanction.UserId, applied.GetProperty("UserId").GetInt32());
        Assert.DoesNotContain(message => message.Contains("private", StringComparison.Ordinal),
            logs.Records.Messages);
    }

    [TestMethod]
    public async Task Report_FailedCommit_EmitsNoConfirmationAndRollsBackAsync()
    {
        // Arrange
        await using var logs = new ServerLogTestHost(fileLevel: "Information");
        var failure = new ReportCommitInterceptor();
        await using GameplayTestDatabase database = await GameplayTestDatabase.CreateAsync();
        using AtariGoDbContext context = database.CreateContext();
        IReadOnlyList<Game> games = await SeedReportsAsync(context);
        DbContextOptionsBuilder<AtariGoDbContext> options =
            new DbContextOptionsBuilder<AtariGoDbContext>()
            .UseSqlite(context.Database.GetDbConnection()).AddInterceptors(failure);
        await using var write = new GameplayTestDbContext(options.Options);
        var reports = new ReportService(write, database.Time,
            logs.Application.Services.GetRequiredService<ILogger<ReportService>>());
        failure.FailCommit = true;
        var transport = new ReportsGrpcService(reports, new TestRoomSessionService(),
            logs.Application.Services.GetRequiredService<ILogger<ReportsGrpcService>>());
        var call = new GameSessionTestCallContext("private-token");
        call.RequestHeaders.Add("authorization", "Bearer host");
        // Act
        await Assert.ThrowsAsync<RpcException>(() => transport.SubmitReport(new SubmitReportRequest
        {
            GameId = games[0].GameId,
            Category = ReportCategory.Chat,
            Description = "private-description"
        }, call));
        List<JsonElement> entries = await logs.ReadEventsAsync(_testContext.CancellationToken);
        // Assert
        Assert.HasCount(1, entries);
        Assert.AreEqual("Unconfirmed", entries[0].GetProperty("Outcome").GetString());
        Assert.AreEqual(0, await context.Reports.CountAsync(_testContext.CancellationToken));
        Assert.AreEqual(0, await context.Sanctions.CountAsync(_testContext.CancellationToken));
    }

    [TestMethod]
    public async Task Leaderboard_FailedQuery_PreservesExceptionAndSafeCauseAsync()
    {
        // Arrange
        await using var logs = new ServerLogTestHost(fileLevel: "Information");
        var failure = new RpcException(new Status(StatusCode.PermissionDenied, "private-failure"));
        var service = new LeaderboardGrpcService(
            new ThrowingLeaderboardService { Failure = failure },
            logs.Application.Services.GetRequiredService<ILogger<LeaderboardGrpcService>>());
        // Act
        RpcException observed = await Assert.ThrowsAsync<RpcException>(() => service.GetTopPlayers(
            new GetTopPlayersRequest(), new GameSessionTestCallContext("private-token")));
        List<JsonElement> entries = await logs.ReadEventsAsync(_testContext.CancellationToken);
        // Assert
        Assert.AreSame(failure, observed);
        Assert.HasCount(1, entries);
        Assert.AreEqual("Rejected", entries[0].GetProperty("Outcome").GetString());
        Assert.AreEqual((int)StatusCode.PermissionDenied,
            entries[0].GetProperty("Error").GetProperty("NativeCode").GetInt32());
        Assert.DoesNotContain(message => message.Contains("private", StringComparison.Ordinal),
            logs.Records.Messages);
    }

    private async Task<IReadOnlyList<Game>> SeedReportsAsync(AtariGoDbContext context)
    {
        var target = new User
        {
            UserName = "privateTarget",
            Email = "privateTarget@example.invalid"
        };
        var games = new List<Game>();
        for (int index = 0; index < 3; index++)
        {
            var reporter = new User
            {
                UserName = "private" + index,
                Email = "private" + index + "@example.invalid"
            };
            var game = new Game
            {
                GameType = "Public",
                State = "InProgress",
                StartedAt = DateTime.UtcNow,
                GameConfiguration = new GameConfiguration { CaptureGoal = 1, TurnTimeSeconds = 30 }
            };
            game.GamePlayers.Add(new GamePlayer
            {
                User = reporter,
                StoneColor = "Black",
                TurnOrder = 1
            });
            game.GamePlayers.Add(new GamePlayer
            {
                User = target,
                StoneColor = "White",
                TurnOrder = 2
            });
            games.Add(game);
        }
        context.Games.AddRange(games);
        await context.SaveChangesAsync(_testContext.CancellationToken);
        return games;
    }

    private static string ReadOperation(JsonElement entry)
    {
        return entry.GetProperty("Operation").GetString()!;
    }
}
