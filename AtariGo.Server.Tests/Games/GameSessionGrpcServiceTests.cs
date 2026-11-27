using Microsoft.Extensions.Logging.Abstractions;

using Grpc.Core;
using Grpc.Net.Client;

using AtariGo.Contracts;
using AtariGo.Server.Application.Games;
using AtariGo.Server.Application.Matchmaking;
using AtariGo.Server.Data;
using AtariGo.Server.Data.Entities;
using AtariGo.Server.Services;
using AtariGo.Server.Tests.Rooms;
using GameSessionSnapshot = AtariGo.Contracts.GameSessionSnapshot;
using ProtoChatRejectionReason = AtariGo.Contracts.ChatMessageRejectionReason;

namespace AtariGo.Server.Tests.Games;

[TestClass]
[TestCategory("Integration")]
public sealed class GameSessionGrpcServiceTests
{
    [TestMethod]
    public async Task Connect_TwoAuthenticatedClients_ShareMovesCapturesAndFinalResultAsync()
    {
        // Arrange
        await using GameplayGrpcTestHost host = await GameplayGrpcTestHost.StartAsync();
        using GrpcChannel channel = GrpcChannel.ForAddress(host.Address);
        using CancellationTokenSource timeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(15));
        GameSession.GameSessionClient client = new GameSession.GameSessionClient(channel);
        using AsyncDuplexStreamingCall<GameSessionClientEvent, GameSessionServerEvent> black =
            client.Connect(Options(host.BlackIdentity.Credential, timeout.Token));
        using AsyncDuplexStreamingCall<GameSessionClientEvent, GameSessionServerEvent> white =
            client.Connect(Options(host.WhiteIdentity.Credential, timeout.Token));
        await black.RequestStream.WriteAsync(Join(host.Database.GameId));
        await white.RequestStream.WriteAsync(Join(host.Database.GameId));
        GameSessionSnapshot start = await ReadSnapshotAsync(black.ResponseStream, "InProgress",
            timeout.Token);
        await ReadSnapshotAsync(white.ResponseStream, "InProgress", timeout.Token);
        // Act
        await SendMoveAsync(black, start.Version, (0, 1));
        GameSessionSnapshot first = await ReadBoardAsync(white.ResponseStream, 2, timeout.Token);
        GameSessionSnapshot whiteTurn = await ConfirmTurnAsync(white, first, timeout.Token);
        await SendMoveAsync(white, whiteTurn.Version, (0, 0));
        GameSessionSnapshot second = await ReadBoardAsync(black.ResponseStream, 3, timeout.Token);
        GameSessionSnapshot blackTurn = await ConfirmTurnAsync(black, second, timeout.Token);
        await SendMoveAsync(black, blackTurn.Version, (1, 0));
        GameSessionSnapshot blackEnd = await ReadSnapshotAsync(black.ResponseStream,
            "Completed", timeout.Token);
        GameSessionSnapshot whiteEnd = await ReadSnapshotAsync(white.ResponseStream,
            "Completed", timeout.Token);
        // Assert
        Assert.AreEqual(GameStoneState.Black, blackEnd.Winner);
        Assert.AreEqual("CaptureGoal", blackEnd.EndReason);
        Assert.AreEqual(1, blackEnd.BlackCaptures);
        Assert.AreEqual(blackEnd.Version, whiteEnd.Version);
        CollectionAssert.AreEqual(blackEnd.Cells.ToArray(), whiteEnd.Cells.ToArray());
        Assert.AreEqual(GameStoneState.Empty, blackEnd.Cells[0].Stone);
    }

    [TestMethod]
    public async Task Connect_WrongGuest_CannotJoinAnotherPlayersGameAsync()
    {
        // Arrange
        await using GameplayGrpcTestHost host = await GameplayGrpcTestHost.StartAsync();
        using GrpcChannel channel = GrpcChannel.ForAddress(host.Address);
        using CancellationTokenSource timeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(10));
        GameSession.GameSessionClient client = new GameSession.GameSessionClient(channel);
        using AsyncDuplexStreamingCall<GameSessionClientEvent, GameSessionServerEvent> call =
            client.Connect(Options("not-a-credential", timeout.Token));
        // Act
        await call.RequestStream.WriteAsync(Join(host.Database.GameId));
        RpcException exception = await Assert.ThrowsAsync<RpcException>(
            () => call.ResponseStream.MoveNext(timeout.Token));
        // Assert
        Assert.AreEqual(StatusCode.Unauthenticated, exception.StatusCode);
    }

    [TestMethod]
    public async Task Connect_FirstEventIsNotJoin_RejectsInvalidArgumentAsync()
    {
        // Arrange
        await using GameplayGrpcTestHost host = await GameplayGrpcTestHost.StartAsync();
        using GrpcChannel channel = GrpcChannel.ForAddress(host.Address);
        using CancellationTokenSource timeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(10));
        GameSession.GameSessionClient client = new GameSession.GameSessionClient(channel);
        using AsyncDuplexStreamingCall<GameSessionClientEvent, GameSessionServerEvent> call =
            client.Connect(Options(host.BlackIdentity.Credential, timeout.Token));

        // Act
        await call.RequestStream.WriteAsync(new GameSessionClientEvent
        {
            SendChatMessage = new SendChatMessage { Text = "Hello" }
        });
        RpcException exception = await Assert.ThrowsAsync<RpcException>(
            () => call.ResponseStream.MoveNext(timeout.Token));

        // Assert
        Assert.AreEqual(StatusCode.InvalidArgument, exception.StatusCode);
    }

    [TestMethod]
    public async Task Connect_TwoCredentialTypes_RejectsInvalidArgumentAsync()
    {
        // Arrange
        await using GameplayGrpcTestHost host = await GameplayGrpcTestHost.StartAsync();
        using GrpcChannel channel = GrpcChannel.ForAddress(host.Address);
        using CancellationTokenSource timeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(10));
        GameSession.GameSessionClient client = new GameSession.GameSessionClient(channel);
        Metadata headers = new Metadata
        {
            { "x-guest-credential", host.BlackIdentity.Credential },
            { "authorization", "Bearer host" }
        };
        CallOptions options = new CallOptions(headers, cancellationToken: timeout.Token);
        using AsyncDuplexStreamingCall<GameSessionClientEvent, GameSessionServerEvent> call =
            client.Connect(options);

        // Act
        await call.RequestStream.WriteAsync(Join(host.Database.GameId));
        RpcException exception = await Assert.ThrowsAsync<RpcException>(
            () => call.ResponseStream.MoveNext(timeout.Token));

        // Assert
        Assert.AreEqual(StatusCode.InvalidArgument, exception.StatusCode);
    }

    [TestMethod]
    public async Task Connect_GuestChat_RejectsMessageWithoutClosingGameAsync()
    {
        // Arrange
        await using GameplayGrpcTestHost host = await GameplayGrpcTestHost.StartAsync();
        using GrpcChannel channel = GrpcChannel.ForAddress(host.Address);
        using CancellationTokenSource timeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(10));
        GameSession.GameSessionClient client = new GameSession.GameSessionClient(channel);
        using AsyncDuplexStreamingCall<GameSessionClientEvent, GameSessionServerEvent> call =
            client.Connect(Options(host.BlackIdentity.Credential, timeout.Token));
        await call.RequestStream.WriteAsync(Join(host.Database.GameId));
        await ReadSnapshotAsync(call.ResponseStream, "Waiting", timeout.Token);

        // Act
        await call.RequestStream.WriteAsync(new GameSessionClientEvent
        {
            SendChatMessage = new SendChatMessage { Text = "Hello" }
        });
        ChatMessageRejected rejection = await ReadChatRejectionAsync(
            call.ResponseStream, timeout.Token);
        using AsyncDuplexStreamingCall<GameSessionClientEvent, GameSessionServerEvent> white =
            client.Connect(Options(host.WhiteIdentity.Credential, timeout.Token));
        await white.RequestStream.WriteAsync(Join(host.Database.GameId));
        GameSessionSnapshot start = await ReadSnapshotAsync(call.ResponseStream,
            "InProgress", timeout.Token);
        await SendMoveAsync(call, start.Version, (0, 1));
        GameSessionSnapshot played = await ReadBoardAsync(white.ResponseStream, 2, timeout.Token);

        // Assert
        Assert.AreEqual(ProtoChatRejectionReason.GuestNotAllowed,
            rejection.Reason);
        Assert.AreEqual(GameStoneState.Black, played.Cells[1].Stone);
    }

    [TestMethod]
    public async Task Connect_ReplacedConnection_ReportsReplacementAndKeepsNewStreamUsableAsync()
    {
        // Arrange
        await using GameplayGrpcTestHost host = await GameplayGrpcTestHost.StartAsync();
        using GrpcChannel channel = GrpcChannel.ForAddress(host.Address);
        using CancellationTokenSource timeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(15));
        GameSession.GameSessionClient client = new GameSession.GameSessionClient(channel);
        using AsyncDuplexStreamingCall<GameSessionClientEvent, GameSessionServerEvent> previous =
            client.Connect(Options(host.BlackIdentity.Credential, timeout.Token));
        await previous.RequestStream.WriteAsync(Join(host.Database.GameId));
        await ReadSnapshotAsync(previous.ResponseStream, "Waiting", timeout.Token);
        // Act
        using AsyncDuplexStreamingCall<GameSessionClientEvent, GameSessionServerEvent> restored =
            client.Connect(Options(host.BlackIdentity.Credential, timeout.Token));
        await restored.RequestStream.WriteAsync(Join(host.Database.GameId));
        await ReadSnapshotAsync(restored.ResponseStream, "Waiting", timeout.Token);
        RpcException replaced = await Assert.ThrowsAsync<RpcException>(
            () => ReadToEndAsync(previous.ResponseStream, timeout.Token));
        using AsyncDuplexStreamingCall<GameSessionClientEvent, GameSessionServerEvent> white =
            client.Connect(Options(host.WhiteIdentity.Credential, timeout.Token));
        await white.RequestStream.WriteAsync(Join(host.Database.GameId));
        GameSessionSnapshot start = await ReadSnapshotAsync(restored.ResponseStream,
            "InProgress", timeout.Token);
        await SendMoveAsync(restored, start.Version, (0, 1));
        GameSessionSnapshot played = await ReadBoardAsync(white.ResponseStream, 2, timeout.Token);
        // Assert
        Assert.AreEqual(StatusCode.AlreadyExists, replaced.StatusCode);
        Assert.AreEqual(GameStoneState.Black, played.Cells[1].Stone);
        Assert.AreEqual("InProgress", played.Status);
    }

    [TestMethod]
    public async Task Connect_TransportFailure_ReportsUnavailableWithoutChangingResultAsync()
    {
        // Arrange
        await using GameplayTestDatabase database = await GameplayTestDatabase.CreateAsync();
        using AtariGoDbContext context = database.CreateContext();
        GuestIdentityRegistry identities = new GuestIdentityRegistry(TimeProvider.System);
        IssuedGuestIdentity identity = identities.Create("Black");
        await AssignGuestAsync(context, identity);
        GameSessionGrpcService service = CreateService(context, database, identities);
        GameSessionTestCallContext callContext = new GameSessionTestCallContext(
            identity.Credential);
        FailingGameSessionTransport transport = new FailingGameSessionTransport(database.GameId,
            new IOException("Simulated read failure."));
        // Act
        RpcException exception = await Assert.ThrowsAsync<RpcException>(
            () => service.Connect(transport, transport, callContext));
        Game progress = await database.ReadAsync();
        // Assert
        Assert.AreEqual(StatusCode.Unavailable, exception.StatusCode);
        Assert.AreEqual("Waiting", progress.State);
        Assert.IsNull(progress.EndReason);
        Assert.IsNull(progress.FinishedAt);
        Assert.IsTrue(progress.GamePlayers.All(player => player.Result == "Pending"));
    }

    [TestMethod]
    public async Task Connect_BothTasksFail_PreservesFirstFailureDuringCleanupAsync()
    {
        // Arrange
        await using GameplayTestDatabase database = await GameplayTestDatabase.CreateAsync();
        using AtariGoDbContext context = database.CreateContext();
        GuestIdentityRegistry identities = new GuestIdentityRegistry(TimeProvider.System);
        IssuedGuestIdentity identity = identities.Create("Black");
        await AssignGuestAsync(context, identity);
        GameSessionGrpcService service = CreateService(context, database, identities);
        GameSessionTestCallContext callContext = new GameSessionTestCallContext(
            identity.Credential);
        FailingGameSessionTransport transport = new FailingGameSessionTransport(database.GameId,
            new RpcException(new Status(StatusCode.InvalidArgument, "Invalid event.")))
        {
            WriteFailure = new IOException("Simulated paired write failure.")
        };
        // Act
        RpcException exception = await Assert.ThrowsAsync<RpcException>(
            () => service.Connect(transport, transport, callContext));
        // Assert
        Assert.AreEqual(StatusCode.InvalidArgument, exception.StatusCode);
    }

    private static GameSessionGrpcService CreateService(AtariGoDbContext context,
        GameplayTestDatabase database, IGuestIdentityRegistry identities)
    {
        return new GameSessionGrpcService(new GameSessionCatalog(context), database.Coordinator,
            identities, new TestRoomSessionService(), NullLogger<GameSessionGrpcService>.Instance);
    }

    private static async Task AssignGuestAsync(AtariGoDbContext context,
        IssuedGuestIdentity identity)
    {
        GamePlayer player = context.GamePlayers.Single(player => player.StoneColor == "Black");
        player.GuestIdentityId = identity.Identity.GuestId;
        await context.SaveChangesAsync();
    }

    private static async Task ReadToEndAsync(IAsyncStreamReader<GameSessionServerEvent> stream,
        CancellationToken cancellationToken)
    {
        while (await stream.MoveNext(cancellationToken))
        {
            // Drain any snapshot queued before the connection was replaced.
        }
    }

    private static CallOptions Options(string credential, CancellationToken cancellationToken)
    {
        return new CallOptions(new Metadata { { "x-guest-credential", credential } },
            cancellationToken: cancellationToken);
    }

    private static GameSessionClientEvent Join(long gameId)
    {
        return new GameSessionClientEvent { JoinGame = new JoinGame { GameId = gameId } };
    }

    private static Task SendMoveAsync(
        AsyncDuplexStreamingCall<GameSessionClientEvent, GameSessionServerEvent> call,
        long version, (int Row, int Column) position)
    {
        return call.RequestStream.WriteAsync(new GameSessionClientEvent
        {
            SubmitMove = new SubmitGameMove
            {
                ActionId = Guid.NewGuid().ToString("N"),
                ExpectedVersion = version,
                Row = position.Row,
                Column = position.Column
            }
        });
    }

    private static async Task<GameSessionSnapshot> ConfirmTurnAsync(
        AsyncDuplexStreamingCall<GameSessionClientEvent, GameSessionServerEvent> call,
        GameSessionSnapshot pending, CancellationToken cancellationToken)
    {
        Assert.IsTrue(pending.TurnConfirmationPending);
        await call.RequestStream.WriteAsync(new GameSessionClientEvent
        {
            AcknowledgeTurn = new AcknowledgeGameTurn
            {
                Version = pending.TurnConfirmationVersion
            }
        });
        while (await call.ResponseStream.MoveNext(cancellationToken))
        {
            GameSessionSnapshot? snapshot = call.ResponseStream.Current.Snapshot;
            if (snapshot is not null && snapshot.Version > pending.Version
                && !snapshot.TurnConfirmationPending)
            {
                return snapshot;
            }
        }
        throw new InvalidOperationException("The turn confirmation was not received.");
    }

    private static async Task<GameSessionSnapshot> ReadSnapshotAsync(
        IAsyncStreamReader<GameSessionServerEvent> stream, string status, CancellationToken token)
    {
        while (await stream.MoveNext(token))
        {
            if (stream.Current.Snapshot?.Status == status)
            {
                return stream.Current.Snapshot;
            }
        }
        throw new InvalidOperationException("The expected snapshot was not received.");
    }

    private static async Task<GameSessionSnapshot> ReadBoardAsync(
        IAsyncStreamReader<GameSessionServerEvent> stream, int turn, CancellationToken token)
    {
        while (await stream.MoveNext(token))
        {
            if (stream.Current.Snapshot?.TurnNumber == turn)
            {
                return stream.Current.Snapshot;
            }
        }
        throw new InvalidOperationException("The expected move was not received.");
    }

    private static async Task<ChatMessageRejected> ReadChatRejectionAsync(
        IAsyncStreamReader<GameSessionServerEvent> stream, CancellationToken token)
    {
        while (await stream.MoveNext(token))
        {
            if (stream.Current.ChatMessageRejected is ChatMessageRejected rejection)
            {
                return rejection;
            }
        }
        throw new InvalidOperationException("The expected chat rejection was not received.");
    }
}
