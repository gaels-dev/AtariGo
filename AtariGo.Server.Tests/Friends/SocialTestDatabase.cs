using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using AtariGo.Server.Data;

namespace AtariGo.Server.Tests.Friends;

internal sealed class SocialTestDatabase : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    private SocialTestDatabase(SqliteConnection connection)
    {
        _connection = connection;
    }

    public static async Task<SocialTestDatabase> CreateAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var database = new SocialTestDatabase(connection);
        await database.CreateSchemaAsync();
        return database;
    }

    public AtariGoDbContext CreateContext()
    {
        DbContextOptions<AtariGoDbContext> options =
            new DbContextOptionsBuilder<AtariGoDbContext>().UseSqlite(_connection).Options;
        return new AtariGoDbContext(options);
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }

    private async Task CreateSchemaAsync()
    {
        await using AtariGoDbContext context = CreateContext();
        // This isolated relational schema tests social queries, not SQL Server DDL compatibility.
        await context.Database.ExecuteSqlRawAsync("""
            CREATE TABLE Users (
                UserId INTEGER PRIMARY KEY, UserName TEXT NOT NULL, Email TEXT NOT NULL,
                State TEXT NOT NULL, RegistrationDate TEXT NOT NULL,
                LastUpdate TEXT NULL, RoleId INTEGER NOT NULL);
            INSERT INTO Users VALUES
                (1, 'Host', 'host@example.test', 'Active', '2026-01-01', NULL, 1),
                (2, 'GuestPlayer', 'player@example.test', 'Active', '2026-01-01', NULL, 1),
                (3, 'Other', 'other@example.test', 'Active', '2026-01-01', NULL, 1);
            CREATE TABLE FriendRequests (
                FriendRequestId INTEGER PRIMARY KEY AUTOINCREMENT,
                RequesterId INTEGER NOT NULL, RecipientId INTEGER NOT NULL,
                State TEXT NOT NULL DEFAULT 'Pending', RequestedAt TEXT NOT NULL,
                RespondedAt TEXT NULL);
            CREATE UNIQUE INDEX PendingPair ON FriendRequests (RequesterId, RecipientId)
                WHERE State = 'Pending';
            CREATE TABLE Friendships (
                UserId INTEGER NOT NULL, FriendUserId INTEGER NOT NULL, CreatedAt TEXT NOT NULL,
                PRIMARY KEY (UserId, FriendUserId));
            """);
    }
}
