using Grpc.Core;

using AtariGo.Contracts;

namespace AtariGo.Client.Services;

public sealed class GrpcRoomInvitationsClient : IRoomInvitationsClient
{
    private readonly RoomInvitations.RoomInvitationsClient _client;
    private readonly string _sessionToken;

    public GrpcRoomInvitationsClient(string sessionToken)
        : this(sessionToken, GrpcClientConnection.Shared.CallInvoker)
    {
    }

    public GrpcRoomInvitationsClient(string sessionToken, CallInvoker invoker)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionToken);
        ArgumentNullException.ThrowIfNull(invoker);
        _sessionToken = sessionToken;
        _client = new RoomInvitations.RoomInvitationsClient(invoker);
    }

    public async Task<SendRoomInvitationResponse> SendAsync(
        SendRoomInvitationRequest request, CancellationToken cancellationToken)
    {
        using AsyncUnaryCall<SendRoomInvitationResponse> call =
            _client.SendRoomInvitationAsync(request, Options(cancellationToken));
        return await call.ResponseAsync;
    }

    public async Task<ListRoomInvitationsResponse> ListAsync(
        int roomId, CancellationToken cancellationToken)
    {
        using AsyncUnaryCall<ListRoomInvitationsResponse> call = _client.ListRoomInvitationsAsync(
            new ListRoomInvitationsRequest { RoomId = roomId }, Options(cancellationToken));
        return await call.ResponseAsync;
    }

    public async Task<RespondRoomInvitationResponse> RespondAsync(
        RespondRoomInvitationRequest request, CancellationToken cancellationToken)
    {
        using AsyncUnaryCall<RespondRoomInvitationResponse> call =
            _client.RespondRoomInvitationAsync(request, Options(cancellationToken));
        return await call.ResponseAsync;
    }

    public async Task<CancelRoomInvitationResponse> CancelAsync(
        long invitationId, CancellationToken token)
    {
        using AsyncUnaryCall<CancelRoomInvitationResponse> call = _client.CancelRoomInvitationAsync(
            new CancelRoomInvitationRequest { InvitationId = invitationId }, Options(token));
        return await call.ResponseAsync;
    }

    private CallOptions Options(CancellationToken token)
    {
        var headers = new Metadata { { "authorization", $"Bearer {_sessionToken}" } };
        return GrpcCallPolicy.CreateOptions(token, headers);
    }
}
