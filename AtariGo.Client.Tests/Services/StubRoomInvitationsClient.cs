using AtariGo.Client.Services;
using AtariGo.Contracts;

namespace AtariGo.Client.Tests.Services;

internal sealed class StubRoomInvitationsClient : IRoomInvitationsClient
{
    public ListRoomInvitationsResponse ListResponse { get; set; } = new();

    public RespondRoomInvitationResponse Response { get; set; } = new();

    public SendRoomInvitationRequest? Sent { get; private set; }

    public RespondRoomInvitationRequest? Decision { get; private set; }

    public int SendCount { get; private set; }

    public int ListCount { get; private set; }

    public Func<Task<SendRoomInvitationResponse>>? SendHandler { get; set; }

    public Func<Task<RespondRoomInvitationResponse>>? RespondHandler { get; set; }

    public Func<Task<ListRoomInvitationsResponse>>? ListHandler { get; set; }

    public Task<SendRoomInvitationResponse> SendAsync(
        SendRoomInvitationRequest request, CancellationToken cancellationToken)
    {
        Sent = request;
        SendCount++;
        return SendHandler is null
            ? Task.FromResult(new SendRoomInvitationResponse()) : SendHandler();
    }

    public Task<ListRoomInvitationsResponse> ListAsync(int roomId,
        CancellationToken cancellationToken)
    {
        ListCount++;
        return ListHandler is null ? Task.FromResult(ListResponse) : ListHandler();
    }

    public Task<RespondRoomInvitationResponse> RespondAsync(
        RespondRoomInvitationRequest request, CancellationToken cancellationToken)
    {
        Decision = request;
        return RespondHandler is null
            ? Task.FromResult(Response) : RespondHandler();
    }

    public Task<CancelRoomInvitationResponse> CancelAsync(
        long invitationId, CancellationToken token)
    {
        return Task.FromResult(new CancelRoomInvitationResponse());
    }
}
