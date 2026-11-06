using System.Runtime.CompilerServices;

using Grpc.Core;

using AtariGo.Contracts;

namespace AtariGo.Client.Services;

public sealed class GrpcRoomsClient : IRoomsClient
{
    private readonly Rooms.RoomsClient _client;
    private readonly GameLifecycle.GameLifecycleClient _gameLifecycle;
    private readonly string _sessionToken;

    public GrpcRoomsClient(string sessionToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionToken);
        _sessionToken = sessionToken;
        CallInvoker invoker = GrpcClientConnection.Shared.CallInvoker;
        _client = new Rooms.RoomsClient(invoker);
        _gameLifecycle = new GameLifecycle.GameLifecycleClient(invoker);
    }

    public async Task<RoomSnapshot> CreatePrivateRoomAsync(
        RoomConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var request = new CreatePrivateRoomRequest { Configuration = configuration };
        using AsyncUnaryCall<CreatePrivateRoomResponse> call = _client.CreatePrivateRoomAsync(
            request, GrpcCallPolicy.CreateOptions(cancellationToken, CreateHeaders()));
        CreatePrivateRoomResponse response = await call.ResponseAsync;
        return response.Room;
    }

    public async Task<JoinRoomResponse> JoinRoomAsync(
        string invitationCode,
        CancellationToken cancellationToken)
    {
        var request = new JoinRoomRequest { InvitationCode = invitationCode };
        using AsyncUnaryCall<JoinRoomResponse> call = _client.JoinRoomAsync(
            request, GrpcCallPolicy.CreateOptions(cancellationToken, CreateHeaders()));
        return await call.ResponseAsync;
    }

    public async Task StartGameAsync(
        StartGameRequest request,
        CancellationToken cancellationToken)
    {
        using AsyncUnaryCall<StartGameResponse> call = _gameLifecycle.StartGameAsync(
            request, GrpcCallPolicy.CreateOptions(cancellationToken, CreateHeaders()));
        await call.ResponseAsync;
    }

    public async Task LeaveRoomAsync(int roomId, CancellationToken cancellationToken)
    {
        var request = new LeaveRoomRequest { RoomId = roomId };
        using AsyncUnaryCall<LeaveRoomResponse> call = _client.LeaveRoomAsync(
            request, GrpcCallPolicy.CreateOptions(cancellationToken, CreateHeaders()));
        await call.ResponseAsync;
    }

    public async IAsyncEnumerable<RoomSnapshot> WatchRoomAsync(
        WatchRoomRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using AsyncServerStreamingCall<RoomSnapshot> call = _client.WatchRoom(
            request,
            new CallOptions(headers: CreateHeaders(), cancellationToken: cancellationToken));

        while (await call.ResponseStream.MoveNext(cancellationToken))
        {
            yield return call.ResponseStream.Current;
        }
    }

    private Metadata CreateHeaders()
    {
        return new Metadata { { "authorization", $"Bearer {_sessionToken}" } };
    }
}
