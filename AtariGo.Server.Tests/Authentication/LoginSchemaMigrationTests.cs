using Microsoft.EntityFrameworkCore;

using AtariGo.Server.Data;
using AtariGo.Server.Data.Entities;
using AtariGo.Server.Tests.Games;

namespace AtariGo.Server.Tests.Authentication;

[TestClass]
[TestCategory("Integration")]
[TestCategory("SqlServer")]
public sealed class LoginSchemaMigrationTests
{
    [TestMethod]
    public async Task Upgrade_LegacySchema_PreservesCredentialsAndCanRunTwice()
    {
        // Arrange
        await using SqlServerTestDatabase database = await SqlServerTestDatabase.CreateAsync();
        await using AtariGoDbContext context = database.CreateContext();
        context.Users.Add(new User
        {
            UserName = "Existing", Email = "existing@example.test", RoleId = 1, State = "Active",
            RegistrationDate = DateTime.UtcNow,
            Credential = new Credential { PasswordHash = "Existing stored hash" }
        });
        await context.SaveChangesAsync();
        await context.Database.ExecuteSqlRawAsync("""
            DROP TABLE dbo.LoginChallenges;
            ALTER TABLE dbo.Credentials DROP CONSTRAINT DF_Credentials_AuthenticationVersion;
            ALTER TABLE dbo.Credentials DROP COLUMN AuthenticationVersion;
            """);
        // Act
        await database.UpgradeLoginVerificationAsync();
        await database.UpgradeLoginVerificationAsync();
        context.ChangeTracker.Clear();
        Credential credential = await context.Credentials.SingleAsync();
        // Assert
        Assert.AreEqual("Existing stored hash", credential.PasswordHash);
        Assert.AreEqual(0L, credential.AuthenticationVersion);
        Assert.AreEqual(0, await context.LoginChallenges.CountAsync());
        Assert.AreEqual(1, await context.Users.CountAsync());
    }
}
