namespace AtariGo.Server.Application.RoomInvitations;

public interface IRoomInvitationNotifier
{
    bool IsUserConnected(int userId);

    void NotifyRoomInvitationsChanged(int userId);
}
