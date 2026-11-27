namespace AtariGo.Server.Application.RoomInvitations;

public sealed record SendRoomInvitationCommand(int RoomId, int RecipientUserId)
{
    public bool SendByEmail
    {
        get; init;
    }

    public string CultureName { get; init; } = string.Empty;
}
