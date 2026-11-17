using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using AtariGo.Server.Application.Authentication;
using AtariGo.Server.Application.Leaderboard;
using AtariGo.Server.Application.Profile;
using AtariGo.Server.Data;

namespace AtariGo.Server.Tests.Profile;

[TestClass]
[TestCategory("Integration")]
public sealed class ProfileServiceTests
{
    [TestMethod]
    public async Task GetAsync_ReturnsProfileWithCompletedWins()
    {
        // Arrange
        await using SqliteConnection connection = await CreateDatabaseAsync();
        await using AtariGoDbContext context = CreateContext(connection);
        ProfileService service = new ProfileService(context, TimeProvider.System);

        // Act
        UserProfileSnapshot? profile = await service.GetAsync(1, CancellationToken.None);

        // Assert
        Assert.IsNotNull(profile);
        Assert.AreEqual("PlayerOne", profile.UserName);
        Assert.AreEqual("first@example.test", profile.Email);
        Assert.AreEqual(1, profile.Wins);
        Assert.AreEqual(1, profile.AvatarId);
    }

    [TestMethod]
    public async Task UpdateAsync_SavesUserNameAndAvatarTogether()
    {
        // Arrange
        await using SqliteConnection connection = await CreateDatabaseAsync();
        await using AtariGoDbContext context = CreateContext(connection);
        ProfileService service = new ProfileService(context, TimeProvider.System);
        ProfileUpdateInput input = new ProfileUpdateInput(1, "  NewName  ", 4);

        // Act
        ProfileUpdateResult result = await service.UpdateAsync(input, CancellationToken.None);
        await using AtariGoDbContext verifyContext = CreateContext(connection);
        UserProfileSnapshot? saved = await new ProfileService(verifyContext,
            TimeProvider.System).GetAsync(1, CancellationToken.None);

        // Assert
        Assert.AreEqual(ProfileUpdateStatus.Updated, result.Status);
        Assert.IsNotNull(saved);
        Assert.AreEqual("NewName", saved.UserName);
        Assert.AreEqual(4, saved.AvatarId);
        Assert.AreEqual("first@example.test", saved.Email);
    }

    [TestMethod]
    public async Task UpdateAsync_RejectsInvalidAndTakenNamesWithoutChangingProfile()
    {
        // Arrange
        await using SqliteConnection connection = await CreateDatabaseAsync();
        await using AtariGoDbContext context = CreateContext(connection);
        ProfileService service = new ProfileService(context, TimeProvider.System);

        // Act
        ProfileUpdateResult reserved = await service.UpdateAsync(
            new ProfileUpdateInput(1, "Guest-Test", 2), CancellationToken.None);
        ProfileUpdateResult taken = await service.UpdateAsync(
            new ProfileUpdateInput(1, "PlayerTwo", 2), CancellationToken.None);
        ProfileUpdateResult invalidAvatar = await service.UpdateAsync(
            new ProfileUpdateInput(1, "NewName", 21), CancellationToken.None);
        UserProfileSnapshot? saved = await service.GetAsync(1, CancellationToken.None);

        // Assert
        Assert.AreEqual(ProfileUpdateStatus.InvalidUserName, reserved.Status);
        Assert.AreEqual(ProfileUpdateStatus.UserNameTaken, taken.Status);
        Assert.AreEqual(ProfileUpdateStatus.InvalidAvatar, invalidAvatar.Status);
        Assert.IsNotNull(saved);
        Assert.AreEqual("PlayerOne", saved.UserName);
        Assert.AreEqual(1, saved.AvatarId);
    }

    [TestMethod]
    public async Task UpdateAsync_RequiresCompletedWinsForLockedAvatars()
    {
        // Arrange
        await using SqliteConnection connection = await CreateDatabaseAsync();
        await using AtariGoDbContext context = CreateContext(connection);
        ProfileService service = new ProfileService(context, TimeProvider.System);

        // Act
        ProfileUpdateResult locked = await service.UpdateAsync(
            new ProfileUpdateInput(1, "NewName", 6), CancellationToken.None);
        for (int gameId = 3; gameId <= 6; gameId++)
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO Games VALUES ({gameId}, 'Completed')");
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO GamePlayers VALUES ({gameId}, {gameId}, 1, 'Won')");
        }
        ProfileUpdateResult unlocked = await service.UpdateAsync(
            new ProfileUpdateInput(1, "NewName", 6), CancellationToken.None);
        ProfileUpdateResult nextLocked = await service.UpdateAsync(
            new ProfileUpdateInput(1, "NewName", 7), CancellationToken.None);
        UserProfileSnapshot? saved = await service.GetAsync(1, CancellationToken.None);

        // Assert
        Assert.AreEqual(ProfileUpdateStatus.AvatarLocked, locked.Status);
        Assert.AreEqual(ProfileUpdateStatus.Updated, unlocked.Status);
        Assert.AreEqual(ProfileUpdateStatus.AvatarLocked, nextLocked.Status);
        Assert.IsNotNull(saved);
        Assert.AreEqual("NewName", saved.UserName);
        Assert.AreEqual(6, saved.AvatarId);
        Assert.AreEqual(5, saved.Wins);
    }

    [TestMethod]
    public async Task LeaderboardService_IncludesSavedAvatar()
    {
        // Arrange
        await using SqliteConnection connection = await CreateDatabaseAsync();
        await using AtariGoDbContext context = CreateContext(connection);
        ProfileService profile = new ProfileService(context, TimeProvider.System);
        await profile.UpdateAsync(new ProfileUpdateInput(1, "PlayerOne", 5),
            CancellationToken.None);
        LeaderboardService leaderboard = new LeaderboardService(context);

        // Act
        IReadOnlyList<PlayerLeaderboardEntry> entries = await leaderboard
            .GetTopPlayersAsync(10, CancellationToken.None);

        // Assert
        Assert.HasCount(1, entries);
        Assert.AreEqual(5, entries[0].AvatarId);
        Assert.AreEqual(1, entries[0].Wins);
    }

    private static AtariGoDbContext CreateContext(SqliteConnection connection)
    {
        DbContextOptions<AtariGoDbContext> options =
            new DbContextOptionsBuilder<AtariGoDbContext>()
                .UseSqlite(connection).Options;
        return new AtariGoDbContext(options);
    }

    private static async Task<SqliteConnection> CreateDatabaseAsync()
    {
        SqliteConnection connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using AtariGoDbContext context = CreateContext(connection);
        await context.Database.ExecuteSqlRawAsync("""
            CREATE TABLE Users (
                UserId INTEGER PRIMARY KEY, UserName TEXT NOT NULL UNIQUE,
                Email TEXT NOT NULL, State TEXT NOT NULL,
                RegistrationDate TEXT NOT NULL, LastUpdate TEXT NULL,
                RoleId INTEGER NOT NULL);
            CREATE TABLE Profiles (
                UserId INTEGER PRIMARY KEY, AvatarId INTEGER NOT NULL,
                Description TEXT NULL, LastUpdate TEXT NULL);
            CREATE TABLE Games (GameId INTEGER PRIMARY KEY, State TEXT NOT NULL);
            CREATE TABLE GamePlayers (
                GamePlayerId INTEGER PRIMARY KEY, GameId INTEGER NOT NULL,
                UserId INTEGER NULL, Result TEXT NOT NULL);
            INSERT INTO Users VALUES
                (1, 'PlayerOne', 'first@example.test', 'Active', '2026-01-01', NULL, 1),
                (2, 'PlayerTwo', 'second@example.test', 'Active', '2026-01-01', NULL, 1);
            INSERT INTO Profiles VALUES (1, 1, NULL, NULL), (2, 1, NULL, NULL);
            INSERT INTO Games VALUES (1, 'Completed'), (2, 'InProgress');
            INSERT INTO GamePlayers VALUES
                (1, 1, 1, 'Won'), (2, 2, 1, 'Won');
            """);
        return connection;
    }
}
