using AtariGo.Client.Models;
using AtariGo.Contracts;

namespace AtariGo.Client.Services;

public interface IPrivateRoomSession : IDisposable
{
    event Action<PrivateRoomSessionUpdate>? Updated;

    RoomSnapshot Room { get; }

    Task WatchAsync(CancellationToken cancellationToken);

    Task StartGameAsync(int captureGoal, CancellationToken cancellationToken);

    Task<bool> LeaveAsync(CancellationToken cancellationToken);
}
