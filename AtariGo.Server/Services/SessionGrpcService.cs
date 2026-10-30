using AtariGo.Contracts;
using AtariGo.Server.Application.Sessions;
using Grpc.Core;

namespace AtariGo.Server.Services;

public sealed class SessionGrpcService : Session.SessionBase
{
    private const string AuthorizationHeader = "authorization";
    private const string BearerPrefix = "Bearer ";

    private readonly ISessionService _sessionService;

    public SessionGrpcService(ISessionService sessionService)
    {
        ArgumentNullException.ThrowIfNull(sessionService);
        _sessionService = sessionService;
    }

    public override async Task<HeartbeatResponse> Heartbeat(
        HeartbeatRequest request,
        ServerCallContext context)
    {
        string token = GetSessionToken(context);
        IReadOnlyList<FriendPresenceEntry>? friends =
            await _sessionService.HeartbeatAsync(
                token,
                context.CancellationToken);

        if (friends is null)
        {
            throw CreateUnauthenticatedException();
        }

        return CreateHeartbeatResponse(friends);
    }

    public override async Task<LogoutResponse> Logout(
        LogoutRequest request,
        ServerCallContext context)
    {
        string token = GetSessionToken(context);
        await _sessionService.LogoutAsync(
            token,
            context.CancellationToken);

        return new LogoutResponse();
    }

    private static string GetSessionToken(ServerCallContext context)
    {
        string? authorization = context.RequestHeaders.GetValue(
            AuthorizationHeader);

        if (authorization is null
            || !authorization.StartsWith(
                BearerPrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            throw CreateUnauthenticatedException();
        }

        string token = authorization[BearerPrefix.Length..].Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            throw CreateUnauthenticatedException();
        }

        return token;
    }

    private static HeartbeatResponse CreateHeartbeatResponse(
        IReadOnlyList<FriendPresenceEntry> friends)
    {
        var response = new HeartbeatResponse();
        foreach (FriendPresenceEntry friend in friends)
        {
            response.Friends.Add(new FriendPresence
            {
                UserId = friend.UserId,
                UserName = friend.UserName,
                IsOnline = friend.IsOnline
            });
        }

        return response;
    }

    private static RpcException CreateUnauthenticatedException()
    {
        return new RpcException(new Status(
            StatusCode.Unauthenticated,
            "La sesión no es válida o ha vencido."));
    }
}
