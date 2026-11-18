namespace AtariGo.Server.Data;

public static class DatabaseValues
{
    public static class AvatarIds
    {
        public const int Default = 1;
        public const int InitialUnlockedMaximum = 5;
        public const int Maximum = 20;
        public const int WinsPerUnlock = 5;
    }

    public static class AccountStates
    {
        public const string Active = "Active";
        public const string Inactive = "Inactive";
    }

    public static class SessionStates
    {
        public const string Active = "Active";
        public const string Revoked = "Revoked";
    }

    public static class FriendRequestStates
    {
        public const string Pending = "Pending";
    }

    public static class RoomStates
    {
        public const string Active = "Active";
    }

    public static class GameTypes
    {
        public const string Private = "Private";
        public const string Public = "Public";
    }

    public static class StoneColors
    {
        public const string Black = "Black";
        public const string White = "White";
    }

    public static class GameStates
    {
        public const string InProgress = "InProgress";
        public const string Completed = "Completed";
    }

    public static class GamePlayerResults
    {
        public const string Won = "Won";
    }

    public static class ReportCategories
    {
        public const string Chat = "Chat";
        public const string Inactivity = "Inactivity";
        public const string Manipulation = "Manipulation";
    }

    public static class SanctionTypes
    {
        public const string ChatMute = "ChatMute";
        public const string TemporarySuspension = "TemporarySuspension";
    }
}
