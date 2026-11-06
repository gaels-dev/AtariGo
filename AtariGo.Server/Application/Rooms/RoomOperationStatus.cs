namespace AtariGo.Server.Application.Rooms;

public enum RoomOperationStatus
{
    Success,
    InvalidConfiguration,
    AlreadyHosting,
    NotFound,
    NotMember,
    Full,
    CannotStart,
    VersionMismatch,
    StartInProgress
}
