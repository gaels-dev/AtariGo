namespace AtariGo.Diagnostics;

public enum LobbyOperation
{
    Unspecified = 0,
    RoomCreated = 4001,
    RoomConfigured = 4002,
    RoomJoined = 4003,
    RoomLeft = 4004,
    RoomClosed = 4005,
    RoomStarted = 4006,
    InvitationCreated = 4010,
    InvitationChanged = 4011,
    CreateRoomRequest = 4020,
    ConfigureRoomRequest = 4021,
    JoinRoomRequest = 4022,
    LeaveRoomRequest = 4023,
    SendInvitationRequest = 4024,
    RespondInvitationRequest = 4025,
    CancelInvitationRequest = 4026,
    SearchQueued = 4030,
    SearchCancelled = 4031,
    MatchPaired = 4032,
    CountdownCancelled = 4033,
    MatchStarted = 4034,
    MatchStartFailed = 4035,
    SearchRequest = 4036,
    RoomObservation = 4040,
    RoomRecovery = 4041
}
