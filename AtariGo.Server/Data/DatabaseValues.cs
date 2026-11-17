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
    }

    public static class SessionStates
    {
        public const string Active = "Active";
        public const string Revoked = "Revoked";
        public const string Expired = "Expired";
    }

    public static class FriendRequestStates
    {
        public const string Pending = "Pending";
        public const string Accepted = "Accepted";
        public const string Rejected = "Rejected";
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

    public static class RoleNames
    {
        public const string Player = "Player";
    }
}
