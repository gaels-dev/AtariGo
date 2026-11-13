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
        cancellationToken.ThrowIfCancellationRequested();
        RoomStartReservationResult result = _rooms.ReserveStart(request);
        if (result.Reservation is null)
        {
            return Task.FromResult(new RoomOperationResult(result.Status, result.Room));
        }

        return Task.FromResult(_rooms.CompleteStart(result.Reservation, SavedGameId));
    }

    public Task<long> StartPublicGameAsync(
        PublicMatchStartRequest request,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(SavedGameId);
    }

}
