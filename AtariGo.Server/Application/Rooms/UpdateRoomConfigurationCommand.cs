namespace AtariGo.Server.Application.Rooms;

public sealed record UpdateRoomConfigurationCommand(
    int RoomId,
    LobbyConfiguration Configuration,
    int ExpectedVersion);
