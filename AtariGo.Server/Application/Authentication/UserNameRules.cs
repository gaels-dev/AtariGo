namespace AtariGo.Server.Application.Authentication;

public static class UserNameRules
{
    public const int MaximumLength = 15;

    private const string ReservedGuestName = "Guest";
    private const string ReservedGuestPrefix = "Guest-";

    public static bool IsValid(string? userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            return false;
        }

        string normalizedUserName = userName.Trim();
        if (normalizedUserName.Length > MaximumLength)
        {
            return false;
        }

        return !string.Equals(normalizedUserName, ReservedGuestName,
                StringComparison.OrdinalIgnoreCase)
            && !normalizedUserName.StartsWith(ReservedGuestPrefix,
                StringComparison.OrdinalIgnoreCase);
    }
}
