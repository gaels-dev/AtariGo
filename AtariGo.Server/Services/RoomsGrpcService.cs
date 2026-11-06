using Grpc.Core;

using AtariGo.Contracts;
using AtariGo.Server.Application.Rooms;
using AtariGo.Server.Application.Sessions;

namespace AtariGo.Server.Services;

public sealed class RoomsGrpcService : Rooms.RoomsBase
{
    private readonly IRoomsService _roomsService;
    private readonly ISessionService _sessionService;

    public RoomsGrpcService(IRoomsService roomsService, ISessionService sessionService)
    {
        ArgumentNullException.ThrowIfNull(roomsService);
        ArgumentNullException.ThrowIfNull(sessionService);

        _roomsService = roomsService;
        _sessionService = sessionService;
    }

    public override async Task<CreatePrivateRoomResponse> CreatePrivateRoom(
        CreatePrivateRoomRequest request,
        ServerCallContext context)
    {
        LobbyParticipant host = await GetParticipantAsync(context);
        if (request.Configuration is null)
        {
            throw new RpcException(new Status(
                StatusCode.InvalidArgument, "Room configuration is required."));
        }

        var configuration = new LobbyConfiguration(
            request.Configuration.CaptureGoal,
            request.Configuration.GameTimeSeconds,
            request.Configuration.TurnTimeSeconds);
        return new CreatePrivateRoomResponse
        {
            Room = MapRoom(RequireRoom(_roomsService.CreatePrivateRoom(host, configuration)))
        };
    }

    public override async Task<JoinRoomResponse> JoinRoom(
        JoinRoomRequest request,
        ServerCallContext context)
    {
        LobbyParticipant participant = await GetParticipantAsync(context);
        var locator = new RoomLocator(request.RoomId, request.InvitationCode);
        PrivateRoom room = RequireRoom(_roomsService.JoinRoom(participant, locator));
        return new JoinRoomResponse
        {
            Room = MapRoom(room),
            AssignedParticipant = MapParticipant(
                participant, room.Host.UserId == participant.UserId)
        };
    }

    public override async Task<LeaveRoomResponse> LeaveRoom(
        LeaveRoomRequest request,
        ServerCallContext context)
    {
        LobbyParticipant participant = await GetParticipantAsync(context);
        return new LeaveRoomResponse
        {
            UpdatedRoom = MapRoom(RequireRoom(
                _roomsService.LeaveRoom(participant.UserId, request.RoomId)))
        };
    }

    public override async Task WatchRoom(
        WatchRoomRequest request,
        IServerStreamWriter<RoomSnapshot> responseStream,
        ServerCallContext context)
    {
        LobbyParticipant participant = await GetParticipantAsync(context);
        var observation = new RoomObservation(
            participant.UserId, request.RoomId, request.FromVersion);
        await foreach (RoomOperationResult result in _roomsService.WatchRoomAsync(
            observation, context.CancellationToken))
        {
            PrivateRoom room = RequireRoom(result);
            await GetParticipantAsync(context);
            await responseStream.WriteAsync(MapRoom(room), context.CancellationToken);
        }
    }

    private async Task<LobbyParticipant> GetParticipantAsync(ServerCallContext context)
    {
        string sessionToken = SessionRequestAuthentication.GetSessionToken(context);
        SessionIdentity? identity = await _sessionService.ValidateSessionAsync(
            sessionToken, context.CancellationToken);
        if (identity is null)
        {
            throw SessionRequestAuthentication.CreateUnauthenticatedException();
        }

        return new LobbyParticipant(identity.UserId, identity.UserName);
    }

    internal static PrivateRoom RequireRoom(RoomOperationResult result)
    {
        if (result.Status != RoomOperationStatus.Success)
        {
            throw result.Status switch
            {
                RoomOperationStatus.InvalidConfiguration => new RpcException(new Status(
                    StatusCode.InvalidArgument, "Invalid room configuration.")),
                RoomOperationStatus.AlreadyHosting => new RpcException(new Status(
                    StatusCode.FailedPrecondition, "Close the existing room first.")),
                RoomOperationStatus.NotFound => new RpcException(new Status(
                    StatusCode.NotFound, "Room not found.")),
                RoomOperationStatus.NotMember => new RpcException(new Status(
                    StatusCode.PermissionDenied, "Room membership is required.")),
                RoomOperationStatus.CannotStart => new RpcException(new Status(
                    StatusCode.FailedPrecondition, "Two participants are required.")),
                RoomOperationStatus.VersionMismatch => new RpcException(new Status(
                    StatusCode.Aborted, "The room version has changed.")),
                RoomOperationStatus.StartInProgress => new RpcException(new Status(
                    StatusCode.FailedPrecondition, "The room is starting or has started.")),
                RoomOperationStatus.Full => new RpcException(new Status(
                    StatusCode.ResourceExhausted, "The room is full.")),
                _ => new RpcException(new Status(StatusCode.Internal, "Room operation failed."))
            };
        }

        return result.Room ?? throw new InvalidOperationException("Room result is missing.");
    }

    internal static RoomSnapshot MapRoom(PrivateRoom room)
    {
        var snapshot = new RoomSnapshot
        {
            RoomId = room.RoomId,
            InvitationCode = room.InvitationCode,
            Configuration = new RoomConfiguration
            {
                CaptureGoal = room.Configuration.CaptureGoal,
                GameTimeSeconds = room.Configuration.GameTimeSeconds,
                TurnTimeSeconds = room.Configuration.TurnTimeSeconds
            },
            Version = room.Version,
            State = RoomState.Waiting
        };

        if (room.IsClosed)
        {
            snapshot.State = RoomState.Closed;
        }

        if (room.GameId is long gameId)
        {
            snapshot.GameId = gameId;
            snapshot.State = RoomState.Started;
        }

        snapshot.Participants.Add(MapParticipant(room.Host, isHost: true));
        if (room.Opponent is not null)
        {
            snapshot.Participants.Add(MapParticipant(room.Opponent, isHost: false));
        }

        return snapshot;
    }

    private static RoomParticipant MapParticipant(LobbyParticipant participant, bool isHost)
    {
        return new RoomParticipant
        {
            UserId = participant.UserId,
            UserName = participant.UserName,
            IsHost = isHost
        };
    }
}
