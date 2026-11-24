namespace AtariGo.Diagnostics;

internal sealed class DiagnosticFileRetention
{
    private readonly DiagnosticFileOptions _options;
    private readonly TimeProvider _timeProvider;

    internal DiagnosticFileRetention(DiagnosticFileOptions options, TimeProvider timeProvider)
    {
        _options = options;
        _timeProvider = timeProvider;
    }

    internal bool Prepare(long requiredBytes, bool allowCleanup)
    {
        FileInfo[] files = new DirectoryInfo(_options.DirectoryPath)
            .GetFiles(_options.FilePrefix + "-*.log").Where(IsOwnedFile)
            .OrderBy(file => file.LastWriteTimeUtc).ToArray();
        foreach (FileInfo file in files)
        {
            file.Refresh();
        }
        long totalBytes = files.Sum(file => file.Length);
        if (!allowCleanup)
        {
            return requiredBytes <= _options.MaximumTotalBytes - totalBytes;
        }
        long cutoff = Math.Max(0, _timeProvider.GetUtcNow().UtcTicks - _options.Retention.Ticks);
        foreach (FileInfo file in files)
        {
            if (file.LastWriteTimeUtc.Ticks > cutoff
                && requiredBytes <= _options.MaximumTotalBytes - totalBytes)
            {
                continue;
            }
            long length = file.Length;
            if (TryDeleteClosedFile(file))
            {
                totalBytes -= length;
            }
        }
        return requiredBytes <= _options.MaximumTotalBytes - totalBytes;
    }

    private bool IsOwnedFile(FileInfo file)
    {
        string prefix = _options.FilePrefix + "-";
        string stem = Path.GetFileNameWithoutExtension(file.Name);
        return !file.Attributes.HasFlag(FileAttributes.ReparsePoint)
            && string.Equals(file.Extension, ".log", StringComparison.Ordinal)
            && stem.StartsWith(prefix, StringComparison.Ordinal)
            && Guid.TryParseExact(stem.AsSpan(prefix.Length), "N", out _);
    }

    private static bool TryDeleteClosedFile(FileInfo file)
    {
        try
        {
            using var lease = new FileStream(file.FullName, FileMode.Open, FileAccess.ReadWrite,
                FileShare.Delete);
            File.Delete(file.FullName);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }
}
