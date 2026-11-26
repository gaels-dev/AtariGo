using Microsoft.EntityFrameworkCore;

using AtariGo.Server.Application.Reports;
using AtariGo.Server.Application.Sessions;
using AtariGo.Server.Data;
using AtariGo.Server.Data.Entities;
using AtariGo.Server.Tests.Games;
using AtariGo.Server.Tests.RoomInvitations;

namespace AtariGo.Server.Tests.Reports;

[TestClass]
[TestCategory("Integration")]
[TestCategory("SqlServer")]
public sealed partial class ReportServiceTests
{
    private const int ReporterCount = 6;

    [TestMethod]
    public async Task SubmitReportAsync_DistinctReporters_CreateProgressiveSuspensions()
    {
        // Arrange
        await using SqlServerTestDatabase database = await SqlServerTestDatabase.CreateAsync();
        await using AtariGoDbContext context = database.CreateContext();
        (User target, List<User> reporters, List<Game> games) = await SeedAsync(context);
        ReportService service = new ReportService(context, TimeProvider.System);

        // Act
        for (int index = 0; index < ReporterCount; index++)
        {
            SessionIdentity identity = new SessionIdentity(
                reporters[index].UserId, reporters[index].UserName);
            SubmitReportCommand command = new SubmitReportCommand(
                games[index].GameId, "Inactivity", null);
            ReportSubmissionResult result = await service.SubmitReportAsync(
                identity, command, CancellationToken.None);
            Assert.AreEqual(ReportSubmissionStatus.Accepted, result.Status);
        }

        // Assert
        List<Sanction> sanctions = await context.Sanctions.AsNoTracking()
            .Where(sanction => sanction.UserId == target.UserId)
            .OrderBy(sanction => sanction.Threshold).ToListAsync();
        Assert.AreEqual(2, sanctions.Count);
        Assert.AreEqual(3, sanctions[0].Threshold);
        Assert.AreEqual(6, sanctions[1].Threshold);
        Assert.AreEqual("TemporarySuspension", sanctions[0].SanctionType);
        Assert.AreEqual(1, (sanctions[0].EndsAt - sanctions[0].StartedAt).TotalDays);
        Assert.AreEqual(3, (sanctions[1].EndsAt - sanctions[1].StartedAt).TotalDays);
        AccountSanctionService access = new AccountSanctionService(
            context, TimeProvider.System);
        Assert.IsTrue(await access.HasActiveSuspensionAsync(
            target.UserId, CancellationToken.None));
    }

    [TestMethod]
    public async Task SubmitReportAsync_DuplicateGameAndActiveMute_AreRejected()
    {
        // Arrange
        await using SqlServerTestDatabase database = await SqlServerTestDatabase.CreateAsync();
        await using AtariGoDbContext context = database.CreateContext();
        (User target, List<User> reporters, List<Game> games) = await SeedAsync(context);
        ReportService service = new ReportService(context, TimeProvider.System);
        for (int index = 0; index < 3; index++)
        {
            await service.SubmitReportAsync(
                new SessionIdentity(reporters[index].UserId, reporters[index].UserName),
                new SubmitReportCommand(games[index].GameId, "Chat", null),
                CancellationToken.None);
        }

        // Act
        ReportSubmissionResult duplicate = await service.SubmitReportAsync(
            new SessionIdentity(reporters[0].UserId, reporters[0].UserName),
            new SubmitReportCommand(games[0].GameId, "Chat", null),
            CancellationToken.None);
        ReportSubmissionResult muted = await service.SubmitReportAsync(
            new SessionIdentity(reporters[3].UserId, reporters[3].UserName),
            new SubmitReportCommand(games[3].GameId, "Chat", null),
            CancellationToken.None);

        // Assert
        Assert.AreEqual(ReportSubmissionStatus.AlreadyReported, duplicate.Status);
        Assert.AreEqual(ReportSubmissionStatus.ChatMuted, muted.Status);
        Assert.AreEqual(3, await context.Reports.CountAsync());
        Assert.AreEqual(1, await context.Sanctions.CountAsync());
        AccountSanctionService access = new AccountSanctionService(
            context, TimeProvider.System);
        Assert.IsFalse(await access.HasActiveSuspensionAsync(
            target.UserId, CancellationToken.None));
    }

    [TestMethod]
    public async Task SubmitReportAsync_WindowReturnsToThree_CreatesAnotherSanction()
    {
        // Arrange
        await using SqlServerTestDatabase database = await SqlServerTestDatabase.CreateAsync();
        await using AtariGoDbContext context = database.CreateContext();
        (_, List<User> reporters, List<Game> games) = await SeedAsync(context);
        ManualTimeProvider time = new ManualTimeProvider();
        ReportService service = new ReportService(context, time);

        // Act
        for (int index = 0; index < ReporterCount; index++)
        {
            if (index == 3)
            {
                time.Advance(TimeSpan.FromDays(31));
            }
            await service.SubmitReportAsync(
                new SessionIdentity(reporters[index].UserId, reporters[index].UserName),
                new SubmitReportCommand(games[index].GameId, "Inactivity", null),
                CancellationToken.None);
        }

        // Assert
        List<Sanction> sanctions = await context.Sanctions.AsNoTracking()
            .OrderBy(sanction => sanction.StartedAt).ToListAsync();
        Assert.AreEqual(2, sanctions.Count);
        Assert.AreEqual(3, sanctions[0].Threshold);
        Assert.AreEqual(3, sanctions[1].Threshold);
        Assert.IsTrue(sanctions[1].StartedAt > sanctions[0].EndsAt);
    }

    [TestMethod]
    public async Task SubmitReportAsync_PreviousLaterEnd_PreservesLaterExpiration()
    {
        // Arrange
        await using SqlServerTestDatabase database = await SqlServerTestDatabase.CreateAsync();
        await using AtariGoDbContext context = database.CreateContext();
        (User target, List<User> reporters, List<Game> games) = await SeedAsync(context);
        ReportService service = new ReportService(context, TimeProvider.System);
        for (int index = 0; index < 2; index++)
        {
            await service.SubmitReportAsync(
                new SessionIdentity(reporters[index].UserId, reporters[index].UserName),
                new SubmitReportCommand(games[index].GameId, "Inactivity", null),
                CancellationToken.None);
        }
        Report firstReport = await context.Reports.OrderBy(report => report.ReportId)
            .FirstAsync();
        DateTime laterEnd = firstReport.CreatedAt.AddDays(7);
        context.Sanctions.Add(new Sanction
        {
            UserId = target.UserId, TriggeringReport = firstReport,
            SanctionType = "TemporarySuspension", Threshold = 9,
            StartedAt = firstReport.CreatedAt, EndsAt = laterEnd
        });
        await context.SaveChangesAsync();

        // Act
        ReportSubmissionResult result = await service.SubmitReportAsync(
            new SessionIdentity(reporters[2].UserId, reporters[2].UserName),
            new SubmitReportCommand(games[2].GameId, "Inactivity", null),
            CancellationToken.None);

        // Assert
        Assert.AreEqual(ReportSubmissionStatus.Accepted, result.Status);
        Sanction latest = await context.Sanctions.AsNoTracking()
            .SingleAsync(sanction => sanction.Threshold == 3);
        Assert.AreEqual(laterEnd, latest.EndsAt);
    }

    private static async Task<(User Target, List<User> Reporters, List<Game> Games)> SeedAsync(
        AtariGoDbContext context)
    {
        DateTime now = DateTime.UtcNow;
        User target = new User
        {
            UserName = "Target", Email = "target@example.test",
            State = "Active", RegistrationDate = now
        };
        List<User> reporters = new List<User>();
        List<Game> games = new List<Game>();
        for (int index = 0; index < ReporterCount; index++)
        {
            User reporter = new User
            {
                UserName = "Reporter" + index,
                Email = "reporter" + index + "@example.test",
                State = "Active", RegistrationDate = now
            };
            reporters.Add(reporter);
            Game game = new Game
            {
                GameConfiguration = new GameConfiguration
                {
                    CaptureGoal = 1, GameTimeSeconds = 600, TurnTimeSeconds = 30
                },
                GameType = "Public", State = "InProgress", StartedAt = now
            };
            game.GamePlayers.Add(new GamePlayer
            {
                User = reporter, StoneColor = "Black", TurnOrder = 1,
                Result = "Pending", JoinedAt = now
            });
            game.GamePlayers.Add(new GamePlayer
            {
                User = target, StoneColor = "White", TurnOrder = 2,
                Result = "Pending", JoinedAt = now
            });
            games.Add(game);
        }
        context.Games.AddRange(games);
        await context.SaveChangesAsync();
        return (target, reporters, games);
    }
}
