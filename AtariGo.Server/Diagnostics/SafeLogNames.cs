namespace AtariGo.Server.Diagnostics;

internal static class SafeLogNames
{
    private const int MaximumLength = 128;

    internal static string Category(string value)
    {
        return IsTechnical(value) && IsKnownRoot(value) ? value : "AtariGo.Server.UnknownSource";
    }

    internal static bool IsTechnical(string? value)
    {
        return !string.IsNullOrEmpty(value) && value.Length <= MaximumLength
            && value.All(character => char.IsAsciiLetterOrDigit(character)
                || character is '.' or '_');
    }

    private static bool IsKnownRoot(string value)
    {
        return value.StartsWith("AtariGo.", StringComparison.Ordinal)
            || value.StartsWith("Microsoft.", StringComparison.Ordinal)
            || value.StartsWith("System.", StringComparison.Ordinal)
            || value.StartsWith("Grpc.", StringComparison.Ordinal);
    }
}
