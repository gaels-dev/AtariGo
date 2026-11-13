namespace AtariGo.Server.Application.Rooms;

public sealed record LobbyConfiguration(
    int CaptureGoal,
    int GameTimeSeconds,
    int TurnTimeSeconds);
