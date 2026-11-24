using AtariGo.Diagnostics;

namespace AtariGo.Server.Diagnostics;

internal static class ServerDiagnosticSettings
{
    internal static DiagnosticFileOptions Read(IConfiguration configuration)
    {
        IConfigurationSection section = configuration.GetSection("Diagnostics");
        string directory = section["DirectoryPath"] ?? Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData), "AtariGo", "logs");
        var defaults = new DiagnosticFileOptions(directory, "server");
        return defaults with
        {
            MaximumFileBytes = section.GetValue("MaximumFileBytes", defaults.MaximumFileBytes),
            MaximumTotalBytes = section.GetValue("MaximumTotalBytes", defaults.MaximumTotalBytes),
            QueueCapacity = section.GetValue("QueueCapacity", defaults.QueueCapacity),
            Retention = section.GetValue("Retention", defaults.Retention),
            RetryDelay = section.GetValue("RetryDelay", defaults.RetryDelay),
            ShutdownTimeout = section.GetValue("ShutdownTimeout", defaults.ShutdownTimeout)
        };
    }
}
