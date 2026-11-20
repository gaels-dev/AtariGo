using AtariGo.GameLogic.Models;

namespace AtariGo.Server.Application.Games;

public static class GameSessionRules
{
    private const string UserKeyPrefix = "user:";
    public const int MaximumPendingEvents = 128;
    public const int BoardSize = GameState.BoardSize;
    public const int PlayerCount = 2;
    public const int MaximumChatMessageLength = 100;
    public const int MaximumChatHistoryCount = 100;
    public const string BlackStoneColor = "Black";
    public const string WhiteStoneColor = "White";

    public static string CreateUserKey(int userId)
    {
        return $"{UserKeyPrefix}{userId}";
    }

    public static bool TryGetUserId(string playerKey, out int userId)
    {
        ArgumentNullException.ThrowIfNull(playerKey);
        userId = 0;
        return playerKey.StartsWith(UserKeyPrefix, StringComparison.Ordinal)
            && int.TryParse(playerKey.AsSpan(UserKeyPrefix.Length), out userId)
            && userId > 0;
    }

    public static string CreateGuestKey(Guid guestIdentityId)
    {
        return $"guest:{guestIdentityId:N}";
    }
}
