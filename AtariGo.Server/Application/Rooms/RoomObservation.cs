namespace AtariGo.Server.Application.Rooms;

public sealed record RoomObservation(int UserId, int RoomId, int FromVersion);
