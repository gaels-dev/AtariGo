namespace AtariGo.Diagnostics;

public sealed record DiagnosticFileOptions
{
    private const long DefaultFileBytes = 5 * 1024 * 1024;
    private const long DefaultTotalBytes = 50 * 1024 * 1024;
    private const int DefaultQueueCapacity = 1000;
    private const int DefaultRetentionDays = 7;
    private const int DefaultRetrySeconds = 1;
    private const int DefaultShutdownSeconds = 5;
    private const int MaximumPrefixLength = 32;
    private static readonly TimeSpan maximumTimerDelay =
        TimeSpan.FromMilliseconds(uint.MaxValue - 1L);

    public DiagnosticFileOptions(string directoryPath, string filePrefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePrefix);
        if (filePrefix.Length > MaximumPrefixLength || !filePrefix.All(IsPrefixCharacter))
        {
            throw new ArgumentException("Use an ASCII alphanumeric file prefix.",
                nameof(filePrefix));
        }
        DirectoryPath = Path.GetFullPath(directoryPath);
        FilePrefix = filePrefix;
    }

    public string DirectoryPath { get; }

    public string FilePrefix { get; }

    public long MaximumFileBytes { get; init; } = DefaultFileBytes;

    public long MaximumTotalBytes { get; init; } = DefaultTotalBytes;

    public int QueueCapacity { get; init; } = DefaultQueueCapacity;

    public TimeSpan Retention { get; init; } = TimeSpan.FromDays(DefaultRetentionDays);

    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(DefaultRetrySeconds);

    public TimeSpan ShutdownTimeout { get; init; } = TimeSpan.FromSeconds(DefaultShutdownSeconds);

    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaximumFileBytes);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaximumTotalBytes, MaximumFileBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(QueueCapacity);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(Retention, TimeSpan.Zero);
        ValidateDelay(RetryDelay);
        ValidateDelay(ShutdownTimeout);
    }

    private static void ValidateDelay(TimeSpan delay)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(delay, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(delay, maximumTimerDelay);
    }

    private static bool IsPrefixCharacter(char character)
    {
        return char.IsAsciiLetterOrDigit(character) || character == '_';
    }
}
