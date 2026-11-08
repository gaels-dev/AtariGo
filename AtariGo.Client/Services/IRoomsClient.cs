using AtariGo.Contracts;

namespace AtariGo.Client.Services;

public interface IRoomsClient
{
    Task<RoomSnapshot> CreatePrivateRoomAsync(
        RoomConfiguration configuration,
        CancellationToken cancellationToken);

    Task<RoomSnapshot> UpdateRoomConfigurationAsync(
        UpdateRoomConfigurationRequest request,
        CancellationToken cancellationToken);

    Task<JoinRoomResponse> JoinRoomAsync(
        string invitationCode,
        CancellationToken cancellationToken);

    Task StartGameAsync(StartGameRequest request, CancellationToken cancellationToken);

    Task LeaveRoomAsync(int roomId, CancellationToken cancellationToken);

    IAsyncEnumerable<RoomSnapshot> WatchRoomAsync(
        WatchRoomRequest request,
        CancellationToken cancellationToken);
}
