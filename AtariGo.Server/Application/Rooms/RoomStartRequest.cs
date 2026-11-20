namespace AtariGo.Server.Application.Rooms;

public sealed record RoomStartRequest(int RoomId, int ExpectedVersion, int UserId);
