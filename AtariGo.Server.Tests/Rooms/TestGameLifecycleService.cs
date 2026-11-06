using AtariGo.Server.Application.Games;
using AtariGo.Server.Application.Matchmaking;
using AtariGo.Server.Application.Rooms;

namespace AtariGo.Server.Tests.Rooms;

internal sealed class TestGameLifecycleService : IGameLifecycleService
{
    public const long SavedGameId = 42;

    private readonly IRoomsService _rooms;

    public TestGameLifecycleService(IRoomsService rooms)
    {
        _rooms = rooms;
    }

    public Task<RoomOperationResult> StartGameAsync(
        RoomStartRequest request,
        CancellationToken cancellationToken)
    {
        return _rooms.StartGameAsync(request, PersistAsync, cancellationToken);
    }

    public Task<long> StartPublicGameAsync(
        PublicMatchStartRequest request,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(SavedGameId);
    }

    private static Task<long> PersistAsync(PrivateRoom room, CancellationToken cancellationToken)
    {
        return Task.FromResult(SavedGameId);
    }
}
