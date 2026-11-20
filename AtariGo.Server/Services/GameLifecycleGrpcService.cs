using Grpc.Core;

using AtariGo.Contracts;
using AtariGo.Server.Application.Games;
using AtariGo.Server.Application.Rooms;
using AtariGo.Server.Application.Sessions;

namespace AtariGo.Server.Services;

public sealed class GameLifecycleGrpcService : GameLifecycle.GameLifecycleBase
{
    private readonly IGameLifecycleService _gameLifecycle;
    private readonly ISessionService _sessionService;

    public GameLifecycleGrpcService(
        IGameLifecycleService gameLifecycle,
        ISessionService sessionService)
    {
        ArgumentNullException.ThrowIfNull(gameLifecycle);
        ArgumentNullException.ThrowIfNull(sessionService);

        _gameLifecycle = gameLifecycle;
        _sessionService = sessionService;
    }

    public override async Task<StartGameResponse> StartGame(
        StartGameRequest request,
        ServerCallContext context)
    {
        string token = SessionRequestAuthentication.GetSessionToken(context);
        SessionIdentity? identity = await _sessionService.ValidateSessionAsync(
            token, context.CancellationToken);
        if (identity is null)
        {
            throw SessionRequestAuthentication.CreateUnauthenticatedException();
        }

        var command = new RoomStartRequest(request.RoomId, request.ExpectedRoomVersion,
            identity.UserId);
        RoomOperationResult result = await _gameLifecycle.StartGameAsync(
            command, context.CancellationToken);
        RoomSnapshot room = RoomsGrpcService.MapRoom(RoomsGrpcService.RequireRoom(result));
        var response = new StartGameResponse
        {
            GameId = room.GameId,
            BoardConfig = room.Configuration,
            State = room.State
        };
        response.Players.AddRange(room.Participants);
        return response;
    }
}
