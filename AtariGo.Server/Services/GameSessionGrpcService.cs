using System.IO;

using Grpc.Core;
using Microsoft.Data.SqlClient;

using AtariGo.Contracts;
using AtariGo.Server.Application.Games;
using AtariGo.Server.Application.Matchmaking;
using AtariGo.Server.Application.Sessions;
using SessionSnapshotModel = AtariGo.Server.Application.Games.GameSessionSnapshot;
using ProtoSessionSnapshot = AtariGo.Contracts.GameSessionSnapshot;

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
            await SendSnapshotsAsync(connection, responseStream, context.CancellationToken);
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

    private static async Task SendSnapshotsAsync(
        GameSessionConnection connection,
        IServerStreamWriter<GameSessionServerEvent> responseStream,
        CancellationToken cancellationToken)
    {
        await foreach (SessionSnapshotModel snapshot in
            connection.Snapshots.ReadAllAsync(cancellationToken))
        {
            GameSessionServerEvent serverEvent = MapSnapshot(snapshot);
            await responseStream.WriteAsync(serverEvent, cancellationToken);
        }
    }

    private static GameSessionServerEvent MapSnapshot(
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
            OpponentDisconnected = snapshot.OpponentDisconnected
        };
        foreach (GameSessionBoardCell cell in snapshot.Cells)
        {
            responseSnapshot.Cells.Add(MapCell(cell));
        }

        return new GameSessionServerEvent { Snapshot = responseSnapshot };
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
