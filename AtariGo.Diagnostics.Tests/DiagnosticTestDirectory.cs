using System.Text.Json;

namespace AtariGo.Diagnostics.Tests;

internal sealed class DiagnosticTestDirectory : IDisposable
{
    internal DiagnosticTestDirectory()
    {
        string parent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
        DirectoryPath = Path.Combine(parent, "AtariGo-Diagnostics-" + Guid.NewGuid());
        if (!string.Equals(Path.GetDirectoryName(DirectoryPath), parent,
            StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Unexpected diagnostic test directory.");
        }
        Directory.CreateDirectory(DirectoryPath);
    }

    internal string DirectoryPath { get; }

    internal DiagnosticFileOptions Options => new DiagnosticFileOptions(DirectoryPath, "audit");

    public void Dispose()
    {
        Directory.Delete(DirectoryPath, recursive: true);
    }

    internal List<JsonElement> ReadEvents()
    {
        var events = new List<JsonElement>();
        foreach (string file in Directory.GetFiles(DirectoryPath, "audit-*.log"))
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            while (reader.ReadLine() is string line)
            {
                using JsonDocument document = JsonDocument.Parse(line);
                events.Add(document.RootElement.Clone());
            }
        }
        return events;
    }

    internal static DiagnosticEvent CreateEntry(int gameId)
    {
        var definition = new DiagnosticEventDefinition(1001, "AtariGo.Client", "GameConnected");
        var details = new DiagnosticEventDetails
        {
            Context = new DiagnosticContext { GameId = gameId },
            Error = DiagnosticError.FromException(new IOException("synthetic-private-payload"))
        };
        return new DiagnosticEvent(definition, details, DateTimeOffset.UnixEpoch);
    }
}
