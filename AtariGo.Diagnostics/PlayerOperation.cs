namespace AtariGo.Diagnostics;

public enum PlayerOperation
{
    Unspecified = 0,
    ReadProfile = 6001,
    UpdateProfile = 6002,
    ProfileSaved = 6003,
    SubmitReport = 6004,
    ReportCommitted = 6005,
    ChatMuteApplied = 6006,
    SuspensionApplied = 6007,
    QueryHistory = 6008,
    QueryLeaderboard = 6009
}
