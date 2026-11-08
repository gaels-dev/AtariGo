namespace AtariGo.Server.Application.RoomInvitations;

public enum RoomInvitationOperationStatus
{
    Success,
    NotFound,
    PermissionDenied,
    NotFriends,
    RecipientOffline,
    RoomUnavailable,
    InvitationUnavailable,
    AlreadyInRoom
}
