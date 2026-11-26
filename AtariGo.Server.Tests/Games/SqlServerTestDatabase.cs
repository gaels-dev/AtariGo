using System.Text.RegularExpressions;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

using AtariGo.Server.Data;

namespace AtariGo.Server.Tests.Games;

internal sealed class SqlServerTestDatabase : IAsyncDisposable
{
    private const int SchemaCommandTimeoutSeconds = 120;
    private const string MasterConnection = @"Server=(localdb)\MSSQLLocalDB;Database=master;"
        + "Integrated Security=true;TrustServerCertificate=true;Connect Timeout=10";
    private readonly string _databaseName = "AtariGoGameplayTest_" + Guid.NewGuid().ToString("N");

    public string ConnectionString => new SqlConnectionStringBuilder(MasterConnection)
    {
        InitialCatalog = _databaseName
    }.ConnectionString;

    public static async Task<SqlServerTestDatabase> CreateAsync()
    {
        SqlServerTestDatabase database = new SqlServerTestDatabase();
        try
        {
            string create = await File.ReadAllTextAsync(ScriptPath("Create-AtariGo.sql"));
            await database.ExecuteAsync(create.Replace("AtariGoDB", database._databaseName), true);
            return database;
        }
        catch
        {
            await database.DisposeAsync();
            throw;
        }
    }

    public AtariGoDbContext CreateContext()
    {
        DbContextOptions<AtariGoDbContext> options = new DbContextOptionsBuilder<AtariGoDbContext>()
            .UseSqlServer(ConnectionString).Options;
        return new AtariGoDbContext(options);
    }

    public async ValueTask DisposeAsync()
    {
        if (!Regex.IsMatch(_databaseName, @"^AtariGoGameplayTest_[a-f0-9]{32}$"))
        {
            throw new InvalidOperationException("Only the isolated test database can be removed.");
        }
        SqlConnection.ClearAllPools();
        await using SqlConnection connection = new SqlConnection(MasterConnection);
        await connection.OpenAsync();
        string sql = $"IF DB_ID(N'{_databaseName}') IS NOT NULL BEGIN "
            + $"ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; "
            + $"DROP DATABASE [{_databaseName}]; END;";
        await using SqlCommand command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private async Task ExecuteAsync(string script, bool master)
    {
        await using SqlConnection connection = new SqlConnection(
            master ? MasterConnection : ConnectionString);
        await connection.OpenAsync();
        foreach (string batch in Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline))
        {
            if (!string.IsNullOrWhiteSpace(batch))
            {
                await using SqlCommand command = new SqlCommand(batch, connection)
                {
                    CommandTimeout = SchemaCommandTimeoutSeconds
                };
                await command.ExecuteNonQueryAsync();
            }
        }
    }

    private static string ScriptPath(string file)
    {
        return Path.Combine(AppContext.BaseDirectory, "DatabaseScripts", file);
    }
}
