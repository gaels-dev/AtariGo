using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using AtariGo.Diagnostics;
using AtariGo.Server.Application.Authentication;
using AtariGo.Server.Data;
using AtariGo.Server.Data.Entities;
using AtariGo.Server.Tests.Games;

namespace AtariGo.Server.Tests.Diagnostics;

[TestClass]
[TestCategory("Integration")]
[TestCategory("SqlServer")]
[OSCondition(OperatingSystems.Windows)]
public sealed class IdentityPersistenceTests
{
    private readonly TestContext _testContext;

    public IdentityPersistenceTests(TestContext testContext)
    {
        ArgumentNullException.ThrowIfNull(testContext);
        _testContext = testContext;
    }

    [TestMethod]
    public async Task RegisterAccount_CommittedAndDuplicateResults_LogWithoutSecretsAsync()
    {
        await using SqlServerTestDatabase database = await SqlServerTestDatabase.CreateAsync();
        await using var host = new ServerLogTestHost(fileLevel: "Information",
            connectionString: database.ConnectionString);
        await using AsyncServiceScope scope = host.Application.Services.CreateAsyncScope();
        AtariGoDbContext context = scope.ServiceProvider.GetRequiredService<AtariGoDbContext>();
        var service = new RegistrationService(context, new PasswordHasher<User>(),
            new RegistrationValidator(),
            scope.ServiceProvider.GetRequiredService<ILogger<RegistrationService>>(),
            TimeProvider.System);
        var command = new RegisterAccountCommand
        {
            UserName = "PrivatePlayer",
            Email = "private@example.invalid",
            Password = "PrivateSecret9",
            PasswordConfirmation = "PrivateSecret9"
        };
        RegistrationStatus created = await service.RegisterAccountAsync(command,
            _testContext.CancellationToken);
        RegistrationStatus duplicate = await service.RegisterAccountAsync(command,
            _testContext.CancellationToken);
        int savedUser = await context.Users.Select(user => user.UserId)
            .SingleAsync(_testContext.CancellationToken);
        await host.Diagnostics.FlushAsync(_testContext.CancellationToken);
        DiagnosticEvent[] events = host.Records.States.OfType<DiagnosticEvent>().Where(entry =>
            entry.Definition.Id == (int)IdentityOperation.RegisterAccount).ToArray();

        Assert.AreEqual(RegistrationStatus.Success, created);
        Assert.AreEqual(RegistrationStatus.UsernameTaken, duplicate);
        Assert.HasCount(2, events);
        Assert.AreEqual(savedUser, events[0].Details.Context!.UserId);
        Assert.AreEqual(DiagnosticOutcome.Succeeded, events[0].Details.Outcome);
        Assert.AreEqual(DiagnosticOutcome.Rejected, events[1].Details.Outcome);
        Assert.IsTrue(host.Records.Messages.All(message =>
            !message.Contains("Private", StringComparison.OrdinalIgnoreCase)));
    }
}
