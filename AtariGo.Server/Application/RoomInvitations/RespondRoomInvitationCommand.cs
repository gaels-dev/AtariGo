namespace AtariGo.Server.Application.RoomInvitations;

public sealed record RespondRoomInvitationCommand(long InvitationId, bool Accept);
