using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

using AtariGo.Server.Application.Profile;
using AtariGo.Server.Data;
using AtariGo.Server.Tests.Games;

namespace AtariGo.Server.Tests.Profile;

[TestClass]
[TestCategory("Integration")]
[TestCategory("SqlServer")]
public sealed class SqlServerProfileMigrationTests
{
    [TestMethod]
    public async Task UpgradeAvatarUnlocks_AvatarRange_AllowsTwentyAndRejectsTwentyOneAsync()
    {
        // Arrange
        await using SqlServerTestDatabase database = await SqlServerTestDatabase.CreateAsync();
        await using AtariGoDbContext context = database.CreateContext();
        await context.Database.ExecuteSqlRawAsync("""
            ALTER TABLE dbo.Profiles DROP CONSTRAINT CK_Profiles_AvatarId;
            ALTER TABLE dbo.Profiles WITH CHECK ADD CONSTRAINT CK_Profiles_AvatarId
                CHECK (AvatarId BETWEEN 1 AND 5);
            INSERT INTO dbo.Users (UserName, Email, RoleId)
            VALUES (N'PlayerOne', N'first@example.test', 1);
            INSERT INTO dbo.Profiles (UserId, AvatarId) VALUES (1, 5);
            """);

        // Act
        await database.UpgradeAvatarUnlocksAsync();
        await database.UpgradeAvatarUnlocksAsync();
        await context.Database.ExecuteSqlRawAsync(
            "UPDATE dbo.Profiles SET AvatarId = 20 WHERE UserId = 1");
        SqlException rejected = await Assert.ThrowsAsync<SqlException>(() =>
            context.Database.ExecuteSqlRawAsync(
                "UPDATE dbo.Profiles SET AvatarId = 21 WHERE UserId = 1"));
        int savedAvatarId = await context.Profiles.AsNoTracking()
            .Where(profile => profile.UserId == 1)
            .Select(profile => profile.AvatarId)
            .SingleAsync();

        // Assert
        Assert.AreEqual(547, rejected.Number);
        Assert.AreEqual(20, savedAvatarId);
    }

    [TestMethod]
    public async Task UpgradeProfile_ExistingUsers_BackfillsAndSupportsReexecutionAsync()
    {
        // Arrange
        await using SqlServerTestDatabase database = await SqlServerTestDatabase.CreateAsync();
        await database.RestoreLegacyProfileSchemaAsync();
        await using (AtariGoDbContext seed = database.CreateContext())
        {
            await seed.Database.ExecuteSqlRawAsync("""
                INSERT INTO dbo.Users (UserName, Email, RoleId)
                VALUES (N'First', N'first@example.test', 1),
                    (N'Second', N'second@example.test', 1);
                INSERT INTO dbo.Profiles (UserId, Avatar)
                SELECT UserId, 0x01 FROM dbo.Users WHERE UserName = N'First';
                """);
        }

        // Act
        await database.UpgradeProfileAsync();
        await database.UpgradeProfileAsync();
        await using AtariGoDbContext context = database.CreateContext();
        List<int> avatars = await context.Profiles.AsNoTracking()
            .OrderBy(profile => profile.UserId)
            .Select(profile => profile.AvatarId)
            .ToListAsync();

        // Assert
        CollectionAssert.AreEqual(new[] { 1, 1 }, avatars);
        int userNameLength = await context.Database.SqlQueryRaw<int>("""
            SELECT max_length / 2 AS Value
            FROM sys.columns
            WHERE object_id = OBJECT_ID(N'dbo.Users') AND name = N'UserName'
            """).SingleAsync();
        Assert.AreEqual(15, userNameLength);
        ProfileService service = new ProfileService(context, TimeProvider.System);
        ProfileUpdateResult result = await service.UpdateAsync(
            new ProfileUpdateInput(1, "First", 5), CancellationToken.None);
        Assert.AreEqual(ProfileUpdateStatus.Updated, result.Status);
    }
}
