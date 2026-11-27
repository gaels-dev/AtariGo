using AtariGo.Server.Application.Rooms;

namespace AtariGo.Server.Application.RoomInvitations;

public interface IRoomInvitationsService
{
    Task<RoomInvitationResult> SendAsync(
        SendRoomInvitationCommand command,
        LobbyParticipant sender,
        CancellationToken cancellationToken);

    Task<RoomInvitationResult> RespondAsync(
        RespondRoomInvitationCommand command,
        LobbyParticipant recipient,
        CancellationToken cancellationToken);
}
