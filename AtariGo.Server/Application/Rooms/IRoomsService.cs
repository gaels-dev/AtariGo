namespace AtariGo.Server.Application.Rooms;

public interface IRoomsService
{
    RoomOperationResult CreatePrivateRoom(
        LobbyParticipant host,
        LobbyConfiguration configuration);

    RoomOperationResult JoinRoom(LobbyParticipant participant, RoomLocator locator);

    RoomOperationResult LeaveRoom(int userId, int roomId);

    Task<RoomOperationResult> StartGameAsync(
        RoomStartRequest request,
        Func<PrivateRoom, CancellationToken, Task<long>> persistGameAsync,
        CancellationToken cancellationToken);

    IAsyncEnumerable<RoomOperationResult> WatchRoomAsync(
        RoomObservation observation,
        CancellationToken cancellationToken);
}
