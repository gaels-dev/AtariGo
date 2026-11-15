using AtariGo.Contracts;

namespace AtariGo.Client.Models;

public sealed record PrivateRoomSessionUpdate(
    RoomSnapshot Room,
    bool IsSynchronized,
    ClientFailureReason Failure)
{
    public bool IsStartRecovery { get; init; }
}
