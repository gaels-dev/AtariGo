using AtariGo.Server.Application.RoomInvitations;

namespace AtariGo.Server.Application.Rooms;

public interface IRoomsService
{
    RoomOperationResult CreatePrivateRoom(
        LobbyParticipant host,
        LobbyConfiguration configuration);

    RoomOperationResult UpdateRoomConfiguration(
        UpdateRoomConfigurationCommand command,
        int userId);

    RoomOperationResult JoinRoom(LobbyParticipant participant, RoomLocator locator);

    RoomInvitationResult SendRoomInvitation(
        SendRoomInvitationCommand command, LobbyParticipant recipient, int senderId);

    IReadOnlyList<RoomInvitationEntry> ListRoomInvitations(int userId, int roomId);

    RoomInvitationResult RespondRoomInvitation(
        RespondRoomInvitationCommand command, LobbyParticipant participant);

    RoomInvitationResult CancelRoomInvitation(long invitationId, int senderId);

    RoomInvitationEntry? FindRoomInvitation(long invitationId, int recipientId);

    void ExpireRoomInvitations();

    RoomOperationResult LeaveRoom(int userId, int roomId);

    Task<RoomOperationResult> StartGameAsync(
        RoomStartRequest request,
        Func<PrivateRoom, CancellationToken, Task<long>> persistGameAsync,
        CancellationToken cancellationToken);

    IAsyncEnumerable<RoomOperationResult> WatchRoomAsync(
        RoomObservation observation,
        CancellationToken cancellationToken);
}
