namespace AtariGo.Contracts.Validation;

public static class UserNameRules
{
    public const int MaximumLength = 15;
    public const string GuestPrefix = "Guest-";

    public static int MaximumGuestDisplayNameLength => GuestPrefix.Length + MaximumLength;

    public static bool IsValid(string? userName)
    {
        return IsValidGuestName(userName) && !IsReserved(userName!.Trim());
    }

    public static bool IsValidGuestName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Any(char.IsControl))
        {
            return false;
        }
        string normalized = name.Trim();
        return normalized.Length <= MaximumLength
            && normalized.All(character => char.IsLetterOrDigit(character) || character == '_');
    }

    public static bool IsReserved(string userName)
    {
        ArgumentNullException.ThrowIfNull(userName);
        return string.Equals(userName, "Guest", StringComparison.OrdinalIgnoreCase)
            || userName.StartsWith(GuestPrefix, StringComparison.OrdinalIgnoreCase);
    }
}
