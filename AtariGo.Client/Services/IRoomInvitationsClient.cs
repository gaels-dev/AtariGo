using AtariGo.Contracts;

namespace AtariGo.Client.Services;

public interface IRoomInvitationsClient
{
    Task<SendRoomInvitationResponse> SendAsync(
        SendRoomInvitationRequest request, CancellationToken cancellationToken);

    Task<ListRoomInvitationsResponse> ListAsync(int roomId, CancellationToken cancellationToken);

    Task<RespondRoomInvitationResponse> RespondAsync(
        RespondRoomInvitationRequest request, CancellationToken cancellationToken);

    Task<CancelRoomInvitationResponse> CancelAsync(long invitationId, CancellationToken token);
}
