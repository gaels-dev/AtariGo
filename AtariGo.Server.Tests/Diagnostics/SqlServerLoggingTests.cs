using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using AtariGo.Diagnostics;
using AtariGo.Server.Data;
using AtariGo.Server.Data.Entities;
using AtariGo.Server.Tests.Games;

namespace AtariGo.Server.Tests.Diagnostics;

[TestClass]
[TestCategory("Integration")]
[TestCategory("SqlServer")]
[OSCondition(OperatingSystems.Windows)]
public sealed class SqlServerLoggingTests
{
    private readonly TestContext _testContext;

    public SqlServerLoggingTests(TestContext testContext)
    {
        ArgumentNullException.ThrowIfNull(testContext);
        _testContext = testContext;
    }

    [TestMethod]
    public async Task SaveChanges_DuplicateEmail_ProtectsAutomaticAndApplicationLogsAsync()
    {
        // Arrange
        await using SqlServerTestDatabase database = await SqlServerTestDatabase.CreateAsync();
        await using var host = new ServerLogTestHost(connectionString: database.ConnectionString);
        ILoggerFactory factory = host.Application.Services.GetRequiredService<ILoggerFactory>();
        await using AsyncServiceScope scope = host.Application.Services.CreateAsyncScope();
        AtariGoDbContext context = scope.ServiceProvider.GetRequiredService<AtariGoDbContext>();
        const string email = "synthetic-private-value@example.invalid";
        context.Users.Add(new User { UserName = "LoggerOne", Email = email });
        await context.SaveChangesAsync(_testContext.CancellationToken);
        context.Users.Add(new User { UserName = "LoggerTwo", Email = email });
        // Act
        DbUpdateException failure = await Assert.ThrowsAsync<DbUpdateException>(() =>
            context.SaveChangesAsync(_testContext.CancellationToken));
        ILogger logger = factory.CreateLogger("AtariGo.Server.Services.ProfileGrpcService");
        logger.LogError(failure, "Profile request failed.");
        await host.Diagnostics.FlushAsync(_testContext.CancellationToken);
        // Assert
        SqlException sql = (SqlException)failure.InnerException!;
        Assert.AreEqual(2627, sql.Number);
        Assert.Contains(email, sql.Message, StringComparison.Ordinal);
        DiagnosticEvent[] events = host.Records.States.Cast<DiagnosticEvent>().ToArray();
        Assert.Contains((DiagnosticEvent entry) => entry.Definition.Source.StartsWith(
            "Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
            && entry.Details.Error?.NativeCode == 2627, events);
        Assert.Contains((DiagnosticEvent entry) => entry.Definition.Source.StartsWith(
            "AtariGo.Server", StringComparison.Ordinal)
            && entry.Details.Error?.NativeCode == 2627, events);
        Assert.IsEmpty(host.Records.Exceptions);
        Assert.IsTrue(host.Records.Messages.All(message =>
            !message.Contains(email, StringComparison.Ordinal)));
        Assert.IsTrue((await host.ReadEventsAsync(_testContext.CancellationToken)).All(entry =>
            !entry.GetRawText().Contains(email, StringComparison.Ordinal)));
    }
}
