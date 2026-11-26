using Microsoft.EntityFrameworkCore;

using AtariGo.Server.Application.Reports;
using AtariGo.Server.Application.Sessions;
using AtariGo.Server.Data;
using AtariGo.Server.Data.Entities;
using AtariGo.Server.Tests.Games;

namespace AtariGo.Server.Tests.Reports;

public sealed partial class ReportServiceTests
{
    [TestMethod]
    [DataRow(199, ReportSubmissionStatus.Accepted)]
    [DataRow(200, ReportSubmissionStatus.Accepted)]
    [DataRow(201, ReportSubmissionStatus.DescriptionTooLong)]
    [DataRow(2000, ReportSubmissionStatus.DescriptionTooLong)]
    public async Task SubmitReportAsync_DescriptionBoundary_PersistsOnlyAllowedLengthsAsync(
        int length, ReportSubmissionStatus expected)
    {
        await using SqlServerTestDatabase database = await SqlServerTestDatabase.CreateAsync();
        await using AtariGoDbContext context = database.CreateContext();
        (_, List<User> reporters, List<Game> games) = await SeedAsync(context);
        ReportService service = new ReportService(context, TimeProvider.System);
        string description = new string('ñ', length);
        SubmitReportCommand command = new SubmitReportCommand(
            games[0].GameId, "Inactivity", description);

        ReportSubmissionResult result = await service.SubmitReportAsync(
            new SessionIdentity(reporters[0].UserId, reporters[0].UserName),
            command, CancellationToken.None);

        Assert.AreEqual(expected, result.Status);
        if (expected == ReportSubmissionStatus.Accepted)
        {
            Report saved = await context.Reports.AsNoTracking().SingleAsync();
            Assert.AreEqual(description, saved.Description);
        }
        else
        {
            Assert.AreEqual(0, await context.Reports.CountAsync());
        }
    }
}
