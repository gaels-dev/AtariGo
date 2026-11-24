using System.IO;
using System.Text.Json;

using AtariGo.Diagnostics;

namespace AtariGo.Client.Tests.Services;

internal sealed class ClientDiagnosticTestDirectory : IDisposable
{
    internal ClientDiagnosticTestDirectory()
    {
        string root = Path.GetFullPath(Path.GetTempPath());
        DirectoryPath = Path.Combine(root, "AtariGo-ClientLogs-" + Guid.NewGuid().ToString("N"));
        if (!string.Equals(Path.GetDirectoryName(DirectoryPath),
            Path.TrimEndingDirectorySeparator(root), StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Diagnostic tests require an isolated temp directory.");
        }
        Directory.CreateDirectory(DirectoryPath);
    }

    internal string DirectoryPath
    {
        get;
    }

    internal DiagnosticFileOptions Options => new DiagnosticFileOptions(DirectoryPath, "client");

    internal List<JsonElement> ReadEvents()
    {
        var entries = new List<JsonElement>();
        foreach (string path in Directory.GetFiles(DirectoryPath, "client-*.log"))
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            while (reader.ReadLine() is string line)
            {
                using JsonDocument document = JsonDocument.Parse(line);
                entries.Add(document.RootElement.Clone());
            }
        }
        return entries;
    }

    public void Dispose()
    {
        Directory.Delete(DirectoryPath, recursive: true);
    }
}
