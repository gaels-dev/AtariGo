using Grpc.Core;
using Microsoft.Data.SqlClient;

using AtariGo.Contracts;
using AtariGo.Server.Application.Games;
using AtariGo.Server.Application.Matchmaking;
using AtariGo.Server.Application.Sessions;
using SessionSnapshotModel = AtariGo.Server.Application.Games.GameSessionSnapshot;
using ProtoSessionSnapshot = AtariGo.Contracts.GameSessionSnapshot;
using ProtoChatRejectionReason = AtariGo.Contracts.ChatMessageRejectionReason;
using DomainChatRejectionReason = AtariGo.Server.Application.Games.ChatMessageRejectionReason;

namespace AtariGo.Server.Services;

public sealed class GameSessionGrpcService : GameSession.GameSessionBase
{
    private const string GuestCredentialHeader = "x-guest-credential";

    private readonly IGameSessionCatalog _gameSessionCatalog;
    private readonly GameCoordinator _coordinator;
    private readonly IGuestIdentityRegistry _guestIdentityRegistry;
    private readonly ISessionService _sessionService;
    private readonly ILogger<GameSessionGrpcService> _logger;

    public GameSessionGrpcService(
        IGameSessionCatalog gameSessionCatalog,
        GameCoordinator gameCoordinator,
        IGuestIdentityRegistry guestIdentityRegistry,
        ISessionService sessionService,
        ILogger<GameSessionGrpcService> logger)
    {
        ArgumentNullException.ThrowIfNull(gameSessionCatalog);
        ArgumentNullException.ThrowIfNull(gameCoordinator);
        ArgumentNullException.ThrowIfNull(guestIdentityRegistry);
        ArgumentNullException.ThrowIfNull(sessionService);
        ArgumentNullException.ThrowIfNull(logger);
        _gameSessionCatalog = gameSessionCatalog;
        _coordinator = gameCoordinator;
        _guestIdentityRegistry = guestIdentityRegistry;
        _sessionService = sessionService;
        _logger = logger;
    }

    public override async Task Connect(
        IAsyncStreamReader<GameSessionClientEvent> requestStream,
        IServerStreamWriter<GameSessionServerEvent> responseStream,
        ServerCallContext context)
    {
        GameSessionConnection? connection = null;
        try
        {
            connection = await OpenConnectionAsync(requestStream, context);
            GameSessionStreamContext streamContext = new GameSessionStreamContext(
                requestStream,
                responseStream,
                context.CancellationToken);
            await RunDuplexStreamAsync(streamContext, connection);
            if (connection.IsReplaced)
            {
                throw new RpcException(new Status(StatusCode.AlreadyExists,
                    "This player opened a newer game connection."));
            }
        }
        catch (OperationCanceledException)
            when (context.CancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation(
                "Game session stream closed by client. GameId: {GameId}",
                connection?.GameId);
        }
        catch (RpcException exception)
            when (context.CancellationToken.IsCancellationRequested
                && exception.StatusCode == StatusCode.Cancelled)
        {
            _logger.LogInformation(
                "Game session RPC cancelled by client. GameId: {GameId}",
                connection?.GameId);
        }
        catch (IOException exception)
            when (context.CancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation(
                exception,
                "Game session transport closed by client. GameId: {GameId}",
                connection?.GameId);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException exception)
        {
            _logger.LogError(exception, "Game persistence failed. GameId: {GameId}",
                connection?.GameId);
            throw new RpcException(new Status(StatusCode.Unavailable,
                "The game action could not be confirmed. Reconnect to recover its result."));
        }
        catch (SqlException exception)
        {
            _logger.LogError(
                exception,
                "Database access failed while connecting to game {GameId}.",
                connection?.GameId);
            throw new RpcException(new Status(
                StatusCode.Unavailable,
                "The game session is temporarily unavailable."));
        }
        finally
        {
            if (connection is not null)
            {
                _coordinator.Disconnect(connection);
            }
        }
    }

    private async Task<GameSessionConnection> OpenConnectionAsync(
        IAsyncStreamReader<GameSessionClientEvent> requestStream, ServerCallContext context)
    {
        GameSessionClientEvent joinEvent = await ReadJoinEventAsync(
            requestStream, context.CancellationToken);
        GameSessionPlayerRequest request = await IdentifyPlayerAsync(joinEvent.JoinGame.GameId,
            context);
        GameSessionDefinition definition = await FindGameAsync(request, context.CancellationToken);
        return await _coordinator.ConnectAsync(definition, context.CancellationToken);
    }

    private static async Task<GameSessionClientEvent> ReadJoinEventAsync(
        IAsyncStreamReader<GameSessionClientEvent> requestStream,
        CancellationToken cancellationToken)
    {
        bool hasEvent = await requestStream.MoveNext(cancellationToken);
        if (!hasEvent
            || requestStream.Current.EventCase
                != GameSessionClientEvent.EventOneofCase.JoinGame
            || requestStream.Current.JoinGame.GameId <= 0)
        {
            throw new RpcException(new Status(
                StatusCode.InvalidArgument,
                "The game session requires a valid join event."));
        }

        return requestStream.Current;
    }

    private async Task<GameSessionPlayerRequest> IdentifyPlayerAsync(
        long gameId,
        ServerCallContext context)
    {
        string? guestCredential = context.RequestHeaders.GetValue(
            GuestCredentialHeader);
        string? sessionToken = context.RequestHeaders.GetValue("authorization");
        if (!string.IsNullOrWhiteSpace(guestCredential)
            && !string.IsNullOrWhiteSpace(sessionToken))
        {
            throw new RpcException(new Status(
                StatusCode.InvalidArgument,
                "Only one game session credential can be provided."));
        }

        if (!string.IsNullOrWhiteSpace(guestCredential))
        {
            return IdentifyGuest(gameId, guestCredential);
        }

        return await IdentifyRegisteredPlayerAsync(
            gameId,
            context,
            context.CancellationToken);
    }

    private GameSessionPlayerRequest IdentifyGuest(
        long gameId,
        string guestCredential)
    {
        GuestIdentity? identity = _guestIdentityRegistry.Find(guestCredential);
        if (identity is null)
        {
            throw SessionRequestAuthentication.CreateUnauthenticatedException();
        }

        return new GameSessionPlayerRequest(gameId, null, identity.GuestId);
    }

    private async Task<GameSessionPlayerRequest> IdentifyRegisteredPlayerAsync(
        long gameId,
        ServerCallContext context,
        CancellationToken cancellationToken)
    {
        string sessionToken = SessionRequestAuthentication.GetSessionToken(context);
        SessionIdentity? identity = await _sessionService.ValidateSessionAsync(
            sessionToken,
            cancellationToken);
        if (identity is null)
        {
            throw SessionRequestAuthentication.CreateUnauthenticatedException();
        }

        return new GameSessionPlayerRequest(gameId, identity.UserId, null);
    }

    private async Task<GameSessionDefinition> FindGameAsync(
        GameSessionPlayerRequest playerRequest,
        CancellationToken cancellationToken)
    {
        GameSessionDefinition? definition = await _gameSessionCatalog.FindForPlayerAsync(
            playerRequest,
            cancellationToken);

        if (definition is null)
        {
            throw new RpcException(new Status(
                StatusCode.PermissionDenied,
                "The game session is unavailable to this player."));
        }

        return definition;
    }

    private async Task RunDuplexStreamAsync(
        GameSessionStreamContext streamContext,
        GameSessionConnection connection)
    {
        using CancellationTokenSource streamCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(
                streamContext.CancellationToken);
        Task receiveTask = ReceiveClientEventsAsync(
            streamContext.RequestStream,
            connection,
            streamCancellation.Token);
        Task sendTask = SendSessionEventsAsync(
            connection,
            streamContext.ResponseStream,
            streamCancellation.Token);
        Task completedTask = await Task.WhenAny(receiveTask, sendTask);
        streamCancellation.Cancel();
        Task remainingTask = ReferenceEquals(completedTask, receiveTask)
            ? sendTask
            : receiveTask;
        try
        {
            await completedTask;
        }
        catch (OperationCanceledException)
            when (streamCancellation.IsCancellationRequested)
        {
        }
        catch (RpcException exception)
            when (streamCancellation.IsCancellationRequested
                && exception.StatusCode == StatusCode.Cancelled)
        {
        }
        catch (IOException)
            when (streamCancellation.IsCancellationRequested)
        {
        }
        finally
        {
            await ObserveCancelledTaskAsync(
                remainingTask,
                streamCancellation.Token);
        }
    }

    private async Task ReceiveClientEventsAsync(
        IAsyncStreamReader<GameSessionClientEvent> requestStream,
        GameSessionConnection connection,
        CancellationToken cancellationToken)
    {
        while (await requestStream.MoveNext(cancellationToken))
        {
            GameSessionClientEvent clientEvent = requestStream.Current;
            if (clientEvent.EventCase
                == GameSessionClientEvent.EventOneofCase.SendChatMessage)
            {
                _coordinator.PublishChatMessage(
                    connection,
                    clientEvent.SendChatMessage.Text);
                continue;
            }

            GameActionCommand command = MapAction(clientEvent);
            await _coordinator.ProcessAsync(connection, command, cancellationToken);
        }
    }

    private static GameActionCommand MapAction(GameSessionClientEvent clientEvent)
    {
        if (clientEvent.EventCase == GameSessionClientEvent.EventOneofCase.SubmitMove)
        {
            SubmitGameMove move = clientEvent.SubmitMove;
            return new GameActionCommand
            {
                ActionId = move.ActionId,
                ExpectedVersion = move.ExpectedVersion,
                Row = move.Row,
                Column = move.Column
            };
        }
        if (clientEvent.EventCase == GameSessionClientEvent.EventOneofCase.Surrender)
        {
            return new GameActionCommand
            {
                ActionId = clientEvent.Surrender.ActionId,
                IsSurrender = true
            };
        }
        throw new RpcException(new Status(StatusCode.InvalidArgument,
            "The game action is unsupported."));
    }

    private static async Task SendSessionEventsAsync(
        GameSessionConnection connection,
        IServerStreamWriter<GameSessionServerEvent> responseStream,
        CancellationToken cancellationToken)
    {
        await foreach (GameSessionDelivery delivery in
            connection.Events.ReadAllAsync(cancellationToken))
        {
            GameSessionServerEvent serverEvent = MapDelivery(delivery);
            await responseStream.WriteAsync(serverEvent, cancellationToken);
        }
    }

    private static async Task ObserveCancelledTaskAsync(
        Task task,
        CancellationToken cancellationToken)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (RpcException exception)
            when (cancellationToken.IsCancellationRequested
                && exception.StatusCode == StatusCode.Cancelled)
        {
        }
        catch (IOException)
            when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private static GameSessionServerEvent MapDelivery(GameSessionDelivery delivery)
    {
        if (delivery.ActionResult is GameActionResult action)
        {
            return new GameSessionServerEvent
            {
                ActionResult = new GameActionResponse
                {
                    ActionId = action.ActionId,
                    IsAccepted = action.IsAccepted,
                    Rejection = action.Rejection,
                    Version = action.Version
                }
            };
        }

        if (delivery.Snapshot is not null)
        {
            return new GameSessionServerEvent
            {
                Snapshot = MapSnapshot(delivery.Snapshot)
            };
        }

        if (delivery.ChatMessage is not null)
        {
            return new GameSessionServerEvent
            {
                ChatMessage = MapChatMessage(
                    delivery.ChatMessage,
                    delivery.RecipientPlayerKey)
            };
        }

        if (delivery.RejectionReason is DomainChatRejectionReason rejectionReason)
        {
            return new GameSessionServerEvent
            {
                ChatMessageRejected = new ChatMessageRejected
                {
                    Reason = MapRejectionReason(rejectionReason)
                }
            };
        }

        throw new InvalidOperationException(
            "The game session delivery does not contain an event.");
    }

    private static ProtoSessionSnapshot MapSnapshot(
        SessionSnapshotModel snapshot)
    {
        ProtoSessionSnapshot responseSnapshot = new ProtoSessionSnapshot
        {
            GameId = snapshot.GameId,
            BoardSize = snapshot.BoardSize,
            CaptureGoal = snapshot.CaptureGoal,
            TurnTimeSeconds = snapshot.TurnTimeSeconds,
            PlayerName = snapshot.Player.DisplayName,
            PlayerStoneColor = snapshot.Player.StoneColor,
            OpponentName = snapshot.Opponent.DisplayName,
            OpponentStoneColor = snapshot.Opponent.StoneColor,
            OpponentConnected = snapshot.OpponentConnected,
            OpponentDisconnected = snapshot.OpponentDisconnected,
            MaximumChatMessageLength = snapshot.MaximumChatMessageLength
        };
        foreach (GameSessionBoardCell cell in snapshot.Cells)
        {
            responseSnapshot.Cells.Add(MapCell(cell));
        }

        foreach (GameSessionChatMessage chatMessage in snapshot.ChatHistory)
        {
            responseSnapshot.ChatHistory.Add(MapChatMessage(
                chatMessage,
                snapshot.Player.PlayerKey));
        }

        GameSessionGameplayMapping.Apply(responseSnapshot, snapshot, snapshot.ServerTimeUtc);
        return responseSnapshot;
    }

    private static GameChatMessage MapChatMessage(
        GameSessionChatMessage chatMessage,
        string recipientPlayerKey)
    {
        return new GameChatMessage
        {
            SenderName = chatMessage.SenderName,
            Text = chatMessage.Text,
            SentAtUnixMilliseconds = chatMessage.SentAtUnixMilliseconds,
            IsSender = chatMessage.SenderPlayerKey == recipientPlayerKey
        };
    }

    private static ProtoChatRejectionReason MapRejectionReason(
        DomainChatRejectionReason reason)
    {
        if (reason == DomainChatRejectionReason.Empty)
        {
            return ProtoChatRejectionReason.Empty;
        }

        return ProtoChatRejectionReason.TooLong;
    }

    private static GameBoardCell MapCell(GameSessionBoardCell cell)
    {
        return new GameBoardCell
        {
            Row = cell.Row,
            Column = cell.Column,
            Stone = MapStone(cell.Stone)
        };
    }

    private static GameStoneState MapStone(string stone)
    {
        if (stone == GameSessionRules.BlackStoneColor)
        {
            return GameStoneState.Black;
        }

        if (stone == GameSessionRules.WhiteStoneColor)
        {
            return GameStoneState.White;
        }

        return GameStoneState.Empty;
    }
}
