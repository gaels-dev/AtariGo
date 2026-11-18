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
        : this(sessionToken, GrpcClientConnection.Shared.CallInvoker)
    {
    }

    public GrpcRoomsClient(string sessionToken, CallInvoker invoker)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionToken);
        ArgumentNullException.ThrowIfNull(invoker);
        _sessionToken = sessionToken;
        _client = new Rooms.RoomsClient(invoker);
        _gameLifecycle = new GameLifecycle.GameLifecycleClient(invoker);
    }

    public async Task<RoomSnapshot> CreatePrivateRoomAsync(
        RoomConfiguration configuration,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var request = new CreatePrivateRoomRequest { Configuration = configuration };
        using AsyncUnaryCall<CreatePrivateRoomResponse> call = _client.CreatePrivateRoomAsync(
            request, Options(cancellationToken));
        CreatePrivateRoomResponse response =
            await GrpcCallPolicy.ExecuteAsync(() => call.ResponseAsync);
        return response.Room;
    }

    public async Task<RoomSnapshot> UpdateRoomConfigurationAsync(
        UpdateRoomConfigurationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        using AsyncUnaryCall<UpdateRoomConfigurationResponse> call =
            _client.UpdateRoomConfigurationAsync(
                request, Options(cancellationToken));
        UpdateRoomConfigurationResponse response =
            await GrpcCallPolicy.ExecuteAsync(() => call.ResponseAsync);
        return response.Room;
    }

    public async Task<JoinRoomResponse> JoinRoomAsync(
        string invitationCode,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invitationCode);
        var request = new JoinRoomRequest { InvitationCode = invitationCode };
        using AsyncUnaryCall<JoinRoomResponse> call = _client.JoinRoomAsync(
            request, Options(cancellationToken));
        return await GrpcCallPolicy.ExecuteAsync(() => call.ResponseAsync);
    }

    public async Task StartGameAsync(
        StartGameRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        using AsyncUnaryCall<StartGameResponse> call = _gameLifecycle.StartGameAsync(
            request, Options(cancellationToken));
        await GrpcCallPolicy.ExecuteAsync(() => call.ResponseAsync);
    }

    public async Task LeaveRoomAsync(int roomId, CancellationToken cancellationToken)
    {
        var request = new LeaveRoomRequest { RoomId = roomId };
        using AsyncUnaryCall<LeaveRoomResponse> call = _client.LeaveRoomAsync(
            request, Options(cancellationToken));
        await GrpcCallPolicy.ExecuteAsync(() => call.ResponseAsync);
    }

    public async IAsyncEnumerable<RoomSnapshot> WatchRoomAsync(
        WatchRoomRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        using AsyncServerStreamingCall<RoomSnapshot> call = _client.WatchRoom(
            request,
            new CallOptions(headers: GrpcCallPolicy.CreateHeaders(_sessionToken),
                cancellationToken: cancellationToken));

        while (await call.ResponseStream.MoveNext(cancellationToken))
        {
            yield return call.ResponseStream.Current;
        }
    }

    private CallOptions Options(CancellationToken cancellationToken)
    {
        return GrpcCallPolicy.CreateOptions(cancellationToken,
            GrpcCallPolicy.CreateHeaders(_sessionToken));
    }
}
