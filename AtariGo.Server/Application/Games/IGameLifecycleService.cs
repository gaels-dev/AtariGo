using AtariGo.Server.Application.Rooms;
using AtariGo.Server.Application.Matchmaking;

namespace AtariGo.Server.Application.Games;

public interface IGameLifecycleService
{
    Task<RoomOperationResult> StartGameAsync(
        RoomStartRequest request,
        CancellationToken cancellationToken);

    Task<long> StartPublicGameAsync(
        PublicMatchStartRequest request,
        CancellationToken cancellationToken);
}
