namespace AtariGo.Server.Application.Rooms;

public sealed record PrivateRoom(
    int RoomId,
    string InvitationCode,
    LobbyConfiguration Configuration)
{
    public required LobbyParticipant Host { get; init; }

    public LobbyParticipant? Opponent { get; init; }

    public int Version { get; init; } = 1;

    public bool IsClosed { get; init; }
}
