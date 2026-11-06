using System.IO;

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
    private readonly IGameSessionRegistry _gameSessionRegistry;
    private readonly IGuestIdentityRegistry _guestIdentityRegistry;
    private readonly ISessionService _sessionService;
    private readonly ILogger<GameSessionGrpcService> _logger;

    public GameSessionGrpcService(
        IGameSessionCatalog gameSessionCatalog,
        IGameSessionRegistry gameSessionRegistry,
        IGuestIdentityRegistry guestIdentityRegistry,
        ISessionService sessionService,
        ILogger<GameSessionGrpcService> logger)
    {
        ArgumentNullException.ThrowIfNull(gameSessionCatalog);
        ArgumentNullException.ThrowIfNull(gameSessionRegistry);
        ArgumentNullException.ThrowIfNull(guestIdentityRegistry);
        ArgumentNullException.ThrowIfNull(sessionService);
        ArgumentNullException.ThrowIfNull(logger);
        _gameSessionCatalog = gameSessionCatalog;
        _gameSessionRegistry = gameSessionRegistry;
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
            GameSessionClientEvent joinEvent = await ReadJoinEventAsync(
                requestStream,
                context.CancellationToken);
            GameSessionPlayerRequest playerRequest = await IdentifyPlayerAsync(
                joinEvent.JoinGame.GameId,
                context);
            GameSessionDefinition definition = await FindGameAsync(
                playerRequest,
                context.CancellationToken);
            connection = _gameSessionRegistry.Connect(definition);
            GameSessionStreamContext streamContext = new GameSessionStreamContext(
                requestStream,
                responseStream,
                context.CancellationToken);
            await RunDuplexStreamAsync(streamContext, connection);
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
                _gameSessionRegistry.Disconnect(connection);
            }
        }
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
        Task receiveTask = ReceiveChatMessagesAsync(
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

    private async Task ReceiveChatMessagesAsync(
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
                _gameSessionRegistry.PublishChatMessage(
                    connection,
                    clientEvent.SendChatMessage.Text);
                continue;
            }

            throw new RpcException(new Status(
                StatusCode.InvalidArgument,
                "Only chat messages are accepted after joining the game."));
        }
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
            GameTimeSeconds = snapshot.GameTimeSeconds,
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
