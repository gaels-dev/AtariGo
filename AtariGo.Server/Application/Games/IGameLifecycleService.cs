using AtariGo.Server.Application.Rooms;

namespace AtariGo.Server.Application.Games;

public interface IGameLifecycleService
{
    Task<RoomOperationResult> StartGameAsync(
        RoomStartRequest request,
        CancellationToken cancellationToken);
}
