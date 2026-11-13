using AtariGo.Server.Application.Rooms;

namespace AtariGo.Server.Application.RoomInvitations;

public sealed record RoomInvitationEntry(long InvitationId, int RoomId, DateTimeOffset ExpiresAt)
{
    public required string RoomCode { get; init; }

    public required LobbyParticipant Sender { get; init; }

    public required LobbyParticipant Recipient { get; init; }

    public RoomInvitationStatus State { get; init; } = RoomInvitationStatus.Pending;
}
