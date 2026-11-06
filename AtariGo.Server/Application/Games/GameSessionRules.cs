using System;

namespace AtariGo.Server.Application.Games;

public static class GameSessionRules
{
    public const int BoardSize = 9;
    public const int PlayerCount = 2;
    public const string BlackStoneColor = "Black";
    public const string WhiteStoneColor = "White";

    public static string CreateUserKey(int userId)
    {
        return $"user:{userId}";
    }

    public static string CreateGuestKey(Guid guestIdentityId)
    {
        return $"guest:{guestIdentityId:N}";
    }
}
