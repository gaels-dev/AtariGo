namespace AtariGo.Server.Application.RoomInvitations;

public sealed record SendRoomInvitationCommand(int RoomId, int RecipientUserId);
