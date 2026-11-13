using AtariGo.Server.Application.Rooms;

namespace AtariGo.Server.Application.RoomInvitations;

public sealed record RoomInvitationResult(
    RoomInvitationOperationStatus Status,
    RoomInvitationEntry? Invitation = null,
    PrivateRoom? Room = null);
