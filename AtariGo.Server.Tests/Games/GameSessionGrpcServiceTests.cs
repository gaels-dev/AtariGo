using Microsoft.VisualStudio.TestTools.UnitTesting;

using Grpc.Core;
using Grpc.Net.Client;

using AtariGo.Contracts;

namespace AtariGo.Server.Tests.Games;

[TestClass]
[TestCategory("Integration")]
public sealed class GameSessionGrpcServiceTests
{
    [TestMethod]
    public async Task Connect_TwoAuthenticatedClients_ShareMovesCapturesAndFinalResult()
    {
        // Arrange
        await using GameplayGrpcTestHost host = await GameplayGrpcTestHost.StartAsync();
        using GrpcChannel channel = GrpcChannel.ForAddress(host.Address);
        using CancellationTokenSource timeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(15));
        GameSession.GameSessionClient client = new GameSession.GameSessionClient(channel);
        using var black = client.Connect(Options(host.BlackIdentity.Credential, timeout.Token));
        using var white = client.Connect(Options(host.WhiteIdentity.Credential, timeout.Token));
        await black.RequestStream.WriteAsync(Join(host.Database.GameId));
        await white.RequestStream.WriteAsync(Join(host.Database.GameId));
        GameSessionSnapshot start = await ReadSnapshotAsync(black.ResponseStream, "InProgress",
            timeout.Token);
        await ReadSnapshotAsync(white.ResponseStream, "InProgress", timeout.Token);
        // Act
        await SendMoveAsync(black, start.Version, (0, 1));
        GameSessionSnapshot first = await ReadBoardAsync(white.ResponseStream, 2, timeout.Token);
        await SendMoveAsync(white, first.Version, (0, 0));
        GameSessionSnapshot second = await ReadBoardAsync(black.ResponseStream, 3, timeout.Token);
        await SendMoveAsync(black, second.Version, (1, 0));
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
    public async Task Connect_WrongGuest_CannotJoinAnotherPlayersGame()
    {
        // Arrange
        await using GameplayGrpcTestHost host = await GameplayGrpcTestHost.StartAsync();
        using GrpcChannel channel = GrpcChannel.ForAddress(host.Address);
        using CancellationTokenSource timeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(10));
        GameSession.GameSessionClient client = new GameSession.GameSessionClient(channel);
        using var call = client.Connect(Options("not-a-credential", timeout.Token));
        // Act
        await call.RequestStream.WriteAsync(Join(host.Database.GameId));
        RpcException exception = await Assert.ThrowsAsync<RpcException>(
            () => call.ResponseStream.MoveNext(timeout.Token));
        // Assert
        Assert.AreEqual(StatusCode.Unauthenticated, exception.StatusCode);
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
                ActionId = Guid.NewGuid().ToString("N"), ExpectedVersion = version,
                Row = position.Row, Column = position.Column
            }
        });
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
}
