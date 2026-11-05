using System.IO;

using Grpc.Core;
using Microsoft.Extensions.Logging;

using AtariGo.Contracts;
using AtariGo.Server.Application.Sessions;

namespace AtariGo.Server.Services;

public sealed class SessionGrpcService : Session.SessionBase
{
    private const string AuthorizationHeader = "authorization";
    private const string BearerPrefix = "Bearer ";

    private readonly ISessionService _sessionService;
    private readonly ISessionConnectionRegistry _connectionRegistry;
    private readonly ILogger<SessionGrpcService> _logger;

    public SessionGrpcService(
        ISessionService sessionService,
        ISessionConnectionRegistry connectionRegistry,
        ILogger<SessionGrpcService> logger)
    {
        ArgumentNullException.ThrowIfNull(sessionService);
        ArgumentNullException.ThrowIfNull(connectionRegistry);
        ArgumentNullException.ThrowIfNull(logger);

        _sessionService = sessionService;
        _connectionRegistry = connectionRegistry;
        _logger = logger;
    }

    /// <summary>Maintains the authenticated player's real-time lobby connection.</summary>
    public override async Task Connect(
        IAsyncStreamReader<SessionClientEvent> requestStream,
        IServerStreamWriter<SessionServerEvent> responseStream,
        ServerCallContext context)
    {
        string sessionToken = GetSessionToken(context);
        await RequireClientConnectionAsync(requestStream, context.CancellationToken);
        SessionConnectionInfo? connectionInfo =
            await _sessionService.OpenConnectionAsync(
                sessionToken,
                context.CancellationToken);
        if (connectionInfo is null)
        {
            throw CreateUnauthenticatedException();
        }

        SessionConnectionLease connection =
            _connectionRegistry.Register(connectionInfo);
        try
        {
            await SendPresenceSnapshotAsync(
                connectionInfo,
                responseStream);
            await RunDuplexStreamAsync(
                requestStream,
                responseStream,
                connection,
                context.CancellationToken);
        }
        finally
        {
            await RemoveConnectionAsync(connection);
        }
    }

    public override async Task<LogoutResponse> Logout(
        LogoutRequest request,
        ServerCallContext context)
    {
        await _sessionService.LogoutAsync(
            GetSessionToken(context),
            context.CancellationToken);
        return new LogoutResponse();
    }

    private async Task RequireClientConnectionAsync(
        IAsyncStreamReader<SessionClientEvent> requestStream,
        CancellationToken cancellationToken)
    {
        bool hasInitialEvent = await requestStream.MoveNext(cancellationToken);
        if (!hasInitialEvent
            || requestStream.Current.EventCase
                != SessionClientEvent.EventOneofCase.ClientConnected)
        {
            throw new RpcException(new Status(
                StatusCode.InvalidArgument,
                "The real-time connection event is required."));
        }
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

    private async Task SendPresenceSnapshotAsync(
        SessionConnectionInfo connectionInfo,
        IServerStreamWriter<SessionServerEvent> responseStream)
    {
        IReadOnlyList<FriendPresenceEntry> friends =
            _connectionRegistry.CreatePresenceSnapshot(connectionInfo);
        var snapshot = new FriendPresenceSnapshot();
        foreach (FriendPresenceEntry friend in friends)
        {
            snapshot.Friends.Add(CreateFriendPresence(friend));
        }

        await responseStream.WriteAsync(new SessionServerEvent
        {
            FriendPresenceSnapshot = snapshot
        });
    }

    private async Task RunDuplexStreamAsync(
        IAsyncStreamReader<SessionClientEvent> requestStream,
        IServerStreamWriter<SessionServerEvent> responseStream,
        SessionConnectionLease connection,
        CancellationToken callCancellationToken)
    {
        using var streamCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            callCancellationToken,
            connection.CancellationToken);
        Task receiveTask = DrainClientEventsAsync(
            requestStream,
            streamCancellation.Token);
        Task sendTask = SendQueuedEventsAsync(
            connection,
            responseStream,
            streamCancellation.Token);

        await Task.WhenAny(receiveTask, sendTask);
        streamCancellation.Cancel();
        await ObserveStreamTasksAsync(
            receiveTask,
            sendTask,
            streamCancellation.Token);
    }

    private static async Task DrainClientEventsAsync(
        IAsyncStreamReader<SessionClientEvent> requestStream,
        CancellationToken cancellationToken)
    {
        while (await requestStream.MoveNext(cancellationToken))
        {
            throw new RpcException(new Status(
                StatusCode.InvalidArgument,
                "No additional client events are supported yet."));
        }
    }

    private static async Task SendQueuedEventsAsync(
        SessionConnectionLease connection,
        IServerStreamWriter<SessionServerEvent> responseStream,
        CancellationToken cancellationToken)
    {
        await foreach (SessionServerEvent serverEvent in
            connection.OutboundEvents.Reader.ReadAllAsync(cancellationToken))
        {
            await responseStream.WriteAsync(serverEvent);
        }
    }

    private async Task ObserveStreamTasksAsync(
        Task receiveTask,
        Task sendTask,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.WhenAll(receiveTask, sendTask);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Real-time session stream stopped by cancellation.");
        }
        catch (RpcException exception)
            when (cancellationToken.IsCancellationRequested
                && exception.StatusCode == StatusCode.Cancelled)
        {
            _logger.LogInformation("Real-time session RPC ended by cancellation.");
        }
        catch (IOException exception)
        {
            _logger.LogInformation(
                exception,
                "Real-time session transport ended when the client closed the stream.");
        }
    }

    private async Task RemoveConnectionAsync(
        SessionConnectionLease connection)
    {
        try
        {
            bool wasCurrentConnection = _connectionRegistry.Remove(connection);
            if (wasCurrentConnection)
            {
                await _sessionService.CloseConnectionAsync(
                    connection.ConnectionInfo.SessionId,
                    CancellationToken.None);
            }
        }
        finally
        {
            connection.Dispose();
        }
    }

    private static FriendPresence CreateFriendPresence(
        FriendPresenceEntry friend)
    {
        return new FriendPresence
        {
            UserId = friend.UserId,
            UserName = friend.UserName,
            IsOnline = friend.IsOnline
        };
    }

    private static RpcException CreateUnauthenticatedException()
    {
        return new RpcException(new Status(
            StatusCode.Unauthenticated,
            "La sesión no es válida o ha vencido."));
    }
}
