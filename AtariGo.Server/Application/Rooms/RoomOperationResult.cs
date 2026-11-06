namespace AtariGo.Server.Application.Rooms;

public sealed record RoomOperationResult(
    RoomOperationStatus Status,
    PrivateRoom? Room = null);
