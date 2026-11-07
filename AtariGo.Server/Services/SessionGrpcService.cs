using System.IO;

using Microsoft.Extensions.Logging;

using Grpc.Core;

using AtariGo.Contracts;
using AtariGo.Server.Application.Friends;
using AtariGo.Server.Application.Sessions;

namespace AtariGo.Server.Services;

public sealed class SessionGrpcService : Session.SessionBase
{
    private readonly ISessionService _sessionService;
    private readonly ISessionConnectionRegistry _connectionRegistry;
    private readonly ILogger<SessionGrpcService> _logger;
    private readonly FriendMutationCoordinator _mutations;

    public SessionGrpcService(
        ISessionService sessionService,
        ISessionConnectionRegistry connectionRegistry,
        ILogger<SessionGrpcService> logger,
        FriendMutationCoordinator mutations)
    {
        ArgumentNullException.ThrowIfNull(sessionService);
        ArgumentNullException.ThrowIfNull(connectionRegistry);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(mutations);

        _sessionService = sessionService;
        _connectionRegistry = connectionRegistry;
        _logger = logger;
        _mutations = mutations;
    }

    private async Task<SessionConnectionLease> OpenConnectionAsync(
        string sessionToken,
        CancellationToken cancellationToken)
    {
        using FriendMutationLease lease = await _mutations.EnterAsync(cancellationToken);
        SessionConnectionInfo? info = await _sessionService.OpenConnectionAsync(
            sessionToken, cancellationToken);
        if (info is null)
        {
            throw SessionRequestAuthentication.CreateUnauthenticatedException();
        }

        return _connectionRegistry.Register(info);
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
        SessionDuplexStreams streams,
        SessionConnectionLease connection,
        CancellationToken callCancellationToken)
    {
        using var streamCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            callCancellationToken,
            connection.CancellationToken);
        Task receiveTask = DrainClientEventsAsync(
            streams.RequestStream,
            streamCancellation.Token);
        Task sendTask = SendQueuedEventsAsync(
            connection,
            streams.ResponseStream,
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

    /// <summary>Maintains the authenticated player's real-time lobby connection.</summary>
    public override async Task Connect(
        IAsyncStreamReader<SessionClientEvent> requestStream,
        IServerStreamWriter<SessionServerEvent> responseStream,
        ServerCallContext context)
    {
        string sessionToken = SessionRequestAuthentication.GetSessionToken(context);
        await RequireClientConnectionAsync(requestStream, context.CancellationToken);
        SessionConnectionLease connection = await OpenConnectionAsync(
            sessionToken, context.CancellationToken);
        try
        {
            await SendPresenceSnapshotAsync(
                connection.ConnectionInfo,
                responseStream);
            await RunDuplexStreamAsync(
                new SessionDuplexStreams(requestStream, responseStream),
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
            SessionRequestAuthentication.GetSessionToken(context),
            context.CancellationToken);
        return new LogoutResponse();
    }
}
