using System.Text.Json;

using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using AtariGo.Server.Diagnostics;
using AtariGo.Server.Data;

namespace AtariGo.Server.Tests.Diagnostics;

internal sealed class ServerLogTestHost : IAsyncDisposable
{
    internal ServerLogTestHost(string level = "Information", string fileLevel = "Warning",
        string? connectionString = null)
    {
        string root = Path.GetFullPath(Path.GetTempPath());
        DirectoryPath = Path.Combine(root, "AtariGo-ServerLogs-" + Guid.NewGuid().ToString("N"));
        if (!string.Equals(Path.GetDirectoryName(DirectoryPath),
            Path.TrimEndingDirectorySeparator(root), StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Server logs require an isolated temp directory.");
        }
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Diagnostics:DirectoryPath"] = DirectoryPath,
            ["Logging:LogLevel:Default"] = level,
            ["Logging:DiagnosticFile:LogLevel:Default"] = fileLevel
        });
        builder.Logging.ClearProviders();
        builder.Services.Configure<LoggerFactoryOptions>(options =>
            options.ActivityTrackingOptions = ActivityTrackingOptions.Baggage);
        builder.Logging.AddProvider(Records);
        if (connectionString is not null)
        {
            builder.Services.AddDbContext<AtariGoDbContext>(options =>
                options.UseSqlServer(connectionString));
        }
        Diagnostics = ServerDiagnostics.Configure(builder);
        Application = builder.Build();
    }

    internal string DirectoryPath
    {
        get;
    }

    internal RecordingLogProvider Records { get; } = new RecordingLogProvider();

    internal WebApplication Application
    {
        get;
    }

    internal ServerDiagnostics Diagnostics
    {
        get;
    }

    internal async Task<List<JsonElement>> ReadEventsAsync(CancellationToken cancellationToken)
    {
        await Diagnostics.FlushAsync(cancellationToken);
        var entries = new List<JsonElement>();
        if (!Directory.Exists(DirectoryPath))
        {
            return entries;
        }
        foreach (string path in Directory.EnumerateFiles(DirectoryPath, "server-*.log"))
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            while (await reader.ReadLineAsync(cancellationToken) is string line)
            {
                using JsonDocument document = JsonDocument.Parse(line);
                entries.Add(document.RootElement.Clone());
            }
        }
        return entries;
    }

    public async ValueTask DisposeAsync()
    {
        await Application.DisposeAsync();
        await Diagnostics.DisposeAsync();
        if (Directory.Exists(DirectoryPath))
        {
            Directory.Delete(DirectoryPath, recursive: true);
        }
    }
}
