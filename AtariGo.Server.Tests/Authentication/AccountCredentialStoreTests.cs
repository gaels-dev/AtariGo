using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

using AtariGo.Server.Application.Authentication;
using AtariGo.Server.Data;
using AtariGo.Server.Data.Entities;
using AtariGo.Server.Tests.PasswordRecovery;

namespace AtariGo.Server.Tests.Authentication;

[TestClass]
[TestCategory("Integration")]
[TestCategory("SqlServer")]
public sealed class AccountCredentialStoreTests
{
    [TestMethod]
    public async Task LockAccountAsync_LegacyCollision_RejectsAmbiguousIdentityAsync()
    {
        await using RecoveryTestHost host = await RecoveryTestHost.CreateAsync();
        DbContextOptions<AtariGoDbContext> options = new DbContextOptionsBuilder<AtariGoDbContext>()
            .UseSqlServer(host.Database.ConnectionString)
            .ConfigureWarnings(ConfigureQueryWarnings)
            .Options;
        await using AtariGoDbContext context = new AtariGoDbContext(options);
        User owner = await context.Users.SingleAsync(user => user.UserName == "Owner");
        User other = await context.Users.SingleAsync(user => user.UserName == "Other");
        owner.Email = "a@b.co";
        other.UserName = owner.Email;
        await context.SaveChangesAsync();
        AccountCredentialStore accounts = new AccountCredentialStore(context);
        await using IDbContextTransaction transaction =
            await context.Database.BeginTransactionAsync();

        User? ambiguous = await accounts.LockAccountAsync("a@b.co", CancellationToken.None);
        User? byName = await accounts.LockAccountAsync("Owner", CancellationToken.None);
        User? byEmail = await accounts.LockAccountAsync("Other@example.test",
            CancellationToken.None);

        Assert.IsNull(ambiguous);
        Assert.AreEqual(owner.UserId, byName?.UserId);
        Assert.AreEqual(other.UserId, byEmail?.UserId);
    }

    private static void ConfigureQueryWarnings(WarningsConfigurationBuilder warnings)
    {
        warnings.Throw(CoreEventId.RowLimitingOperationWithoutOrderByWarning);
    }
}
