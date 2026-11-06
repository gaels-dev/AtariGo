using AtariGo.Contracts;

namespace AtariGo.Client.Services;

public interface IRoomsClient
{
    Task<RoomSnapshot> CreatePrivateRoomAsync(
        RoomConfiguration configuration,
        CancellationToken cancellationToken);

    Task<JoinRoomResponse> JoinRoomAsync(
        string invitationCode,
        CancellationToken cancellationToken);

    Task LeaveRoomAsync(int roomId, CancellationToken cancellationToken);

    IAsyncEnumerable<RoomSnapshot> WatchRoomAsync(
        WatchRoomRequest request,
        CancellationToken cancellationToken);
}
