using Microsoft.Extensions.Logging;

using Grpc.Core;

using AtariGo.Contracts;
using AtariGo.Server.Application.Reports;
using AtariGo.Server.Data;
using AtariGo.Server.Services;
using AtariGo.Server.Tests.Games;
using AtariGo.Server.Tests.Rooms;

namespace AtariGo.Server.Tests.Reports;

[TestClass]
[TestCategory("Integration")]
public sealed class ReportsGrpcServiceTests
{
    [TestMethod]
    public async Task SubmitReport_TooLongDescription_LogsSafeLengthAndReturnsSpecificResultAsync()
    {
        await using GameplayTestDatabase database = await GameplayTestDatabase.CreateAsync();
        await using AtariGoDbContext context = database.CreateContext();
        using GameStreamLogProvider logs = new GameStreamLogProvider();
        using ILoggerFactory loggerFactory = new LoggerFactory();
        loggerFactory.AddProvider(logs);
        ReportsGrpcService service = new ReportsGrpcService(
            new ReportService(context, TimeProvider.System), new TestRoomSessionService(),
            loggerFactory.CreateLogger<ReportsGrpcService>());
        GameSessionTestCallContext call = new GameSessionTestCallContext("private-credential");
        call.RequestHeaders.Add("authorization", "Bearer host");
        string description = "private-report-content" + new string('x', 200);

        SubmitReportResponse result = await service.SubmitReport(new SubmitReportRequest
        {
            GameId = 123,
            Category = ReportCategory.Inactivity,
            Description = description
        }, call);

        Assert.AreEqual(ReportResult.DescriptionTooLong, result.Result);
        Assert.AreEqual(0, result.ReportId);
        Assert.HasCount(1, logs.Entries);
        (LogLevel level, string message, Exception? exception) = logs.Entries.Single();
        Assert.AreEqual(LogLevel.Information, level);
        Assert.IsNull(exception);
        Assert.IsTrue(message.Contains("DescriptionTooLong", StringComparison.Ordinal));
        Assert.IsTrue(message.Contains("DescriptionLength: " + description.Length,
            StringComparison.Ordinal));
        Assert.IsTrue(message.Contains("MaximumDescriptionLength: 200", StringComparison.Ordinal));
        Assert.IsFalse(message.Contains(description, StringComparison.Ordinal));
        Assert.IsFalse(message.Contains("private-credential", StringComparison.Ordinal));
        Assert.IsFalse(message.Contains("Bearer", StringComparison.Ordinal));
        Assert.IsFalse(message.Contains("Host", StringComparison.Ordinal));
        Assert.IsFalse(message.Contains("UserId", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task SubmitReport_TechnicalFailure_LogsTypeWithoutPrivateExceptionContentAsync()
    {
        using GameStreamLogProvider logs = new GameStreamLogProvider();
        using ILoggerFactory loggerFactory = new LoggerFactory();
        loggerFactory.AddProvider(logs);
        FailingReportService reports = new FailingReportService
        {
            Failure = new InvalidOperationException("private-error-content")
        };
        ReportsGrpcService service = new ReportsGrpcService(reports,
            new TestRoomSessionService(), loggerFactory.CreateLogger<ReportsGrpcService>());
        GameSessionTestCallContext call = new GameSessionTestCallContext("private-credential");
        call.RequestHeaders.Add("authorization", "Bearer host");
        Task SubmitAsync()
        {
            return service.SubmitReport(new SubmitReportRequest
            {
                GameId = 1, Category = ReportCategory.Chat, Description = "private-report-content"
            }, call);
        }

        RpcException failure = await Assert.ThrowsAsync<RpcException>(SubmitAsync);

        Assert.AreEqual(StatusCode.Internal, failure.StatusCode);
        (LogLevel level, string message, Exception? exception) = logs.Entries.Single();
        Assert.AreEqual(LogLevel.Error, level);
        Assert.IsNull(exception);
        Assert.IsTrue(message.Contains("InvalidOperationException", StringComparison.Ordinal));
        Assert.IsFalse(message.Contains("private-", StringComparison.Ordinal));
        Assert.IsFalse(failure.Status.Detail.Contains("private-", StringComparison.Ordinal));
    }
}
