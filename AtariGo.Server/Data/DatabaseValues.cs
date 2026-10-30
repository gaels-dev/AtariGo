namespace AtariGo.Server.Data;

/// <summary>Defines the string values persisted by the database for status columns.</summary>
public static class DatabaseValues
{
    public static class AccountStates
    {
        public const string Active = "Active";
    }

    public static class SessionStates
    {
        public const string Active = "Active";
    }

    public static class GameStates
    {
        public const string Completed = "Completed";
    }

    public static class GamePlayerResults
    {
        public const string Won = "Won";
    }

    public static class RoleNames
    {
        public const string Player = "Player";
    }
}
