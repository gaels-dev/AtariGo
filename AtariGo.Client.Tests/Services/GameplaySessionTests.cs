using System.IO;

using Grpc.Core;

using AtariGo.Client.Models;
using AtariGo.Client.Services;
using AtariGo.Contracts;

namespace AtariGo.Client.Tests.Services;

[TestClass]
[TestCategory("Unit")]
public sealed class GameplaySessionTests
{
    [TestMethod]
    public async Task SubmitMoveAsync_ConcurrentActions_QueuesOnlyOneActionAsync()
    {
        // Arrange
        StubGameSessionStream stream = new StubGameSessionStream();
        await using GameplaySessionTestContext context = new GameplaySessionTestContext(stream);
        await context.StartAsync();
        // Act
        await Task.WhenAll(
            Task.Run(() => context.Session.SubmitMoveAsync(0, 0, CancellationToken.None)),
            Task.Run(() => context.Session.SubmitMoveAsync(0, 1, CancellationToken.None)));
        // Assert
        Assert.AreEqual(1, stream.Sent.Count);
        Assert.IsTrue(context.Updates.Last().HasPendingAction);
    }

    [TestMethod]
    public async Task
        SubmitMoveAsync_Reconnect_WaitsForValidSnapshotAndPreservesOriginalActionAsync()
    {
        // Arrange
        StubGameSessionStream first = new StubGameSessionStream { FailSend = true };
        StubGameSessionStream second = new StubGameSessionStream();
        await using GameplaySessionTestContext context = new GameplaySessionTestContext(
            first, second);
        await context.StartAsync(3);
        // Act
        await context.Session.SubmitMoveAsync(0, 1, CancellationToken.None);
        await GameplaySessionTestContext.WaitUntilAsync(() => context.Client.ConnectCount == 2);
        GameSessionSnapshot invalid = GameSessionTestData.Snapshot(4);
        invalid.Cells.RemoveAt(0);
        second.Publish(new GameSessionServerEvent { Snapshot = GameSessionTestData.Snapshot(2) });
        second.Publish(new GameSessionServerEvent { Snapshot = invalid });
        await GameplaySessionTestContext.WaitUntilAsync(() =>
            context.Updates.Any(update => update.InvalidSnapshot));
        bool waitedForValidSnapshot = second.Sent.IsEmpty;
        second.Publish(new GameSessionServerEvent { Snapshot = GameSessionTestData.Snapshot(4) });
        await GameplaySessionTestContext.WaitUntilAsync(() => second.Sent.Count == 1);
        // Assert
        Assert.IsTrue(waitedForValidSnapshot);
        Assert.AreEqual(first.Sent.Single(), second.Sent.Single());
        Assert.AreEqual(3, second.Sent.Single().SubmitMove.ExpectedVersion);
    }

    [TestMethod]
    public async Task SubmitMoveAsync_ConnectionFailsDuringWrite_UnblocksAndReplaysAsync()
    {
        // Arrange
        StubGameSessionStream first = new StubGameSessionStream
        {
            SendCompletion = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously)
        };
        StubGameSessionStream second = new StubGameSessionStream();
        await using GameplaySessionTestContext context = new GameplaySessionTestContext(
            first, second);
        await context.StartAsync();

        // Act
        Task pendingWrite = context.Session.SubmitMoveAsync(0, 1, CancellationToken.None);
        await GameplaySessionTestContext.WaitUntilAsync(() => first.Sent.Count == 1);
        first.Fail(new IOException("The network stopped."));
        await pendingWrite.WaitAsync(TimeSpan.FromSeconds(5));
        await GameplaySessionTestContext.WaitUntilAsync(() => context.Client.ConnectCount == 2);
        second.Publish(new GameSessionServerEvent
        {
            Snapshot = GameSessionTestData.Snapshot(2)
        });
        await GameplaySessionTestContext.WaitUntilAsync(() => second.Sent.Count == 1);

        // Assert
        Assert.AreEqual(first.Sent.Single().SubmitMove.ActionId,
            second.Sent.Single().SubmitMove.ActionId);
        Assert.IsTrue(context.Updates.Any(update =>
            update.State == GameplayConnectionState.Recovering));
    }

    [TestMethod]
    public async Task SubmitMoveAsync_UnrelatedReceipt_KeepsPendingActionUntilMatchingReceiptAsync()
    {
        // Arrange
        StubGameSessionStream stream = new StubGameSessionStream();
        await using GameplaySessionTestContext context = new GameplaySessionTestContext(stream);
        await context.StartAsync();
        await context.Session.SubmitMoveAsync(0, 0, CancellationToken.None);
        GameSessionClientEvent sent = stream.Sent.Single();
        // Act
        stream.Publish(ActionResult("another-action"));
        await GameplaySessionTestContext.WaitUntilAsync(() => context.Updates.Any(update =>
            update.ServerEvent?.ActionResult?.ActionId == "another-action"));
        await context.Session.SubmitMoveAsync(0, 1, CancellationToken.None);
        bool blockedSecondAction = stream.Sent.Count == 1;
        stream.Publish(ActionResult(sent.SubmitMove.ActionId));
        await GameplaySessionTestContext.WaitUntilAsync(() =>
            !context.Updates.Last().HasPendingAction);
        await context.Session.SubmitMoveAsync(0, 1, CancellationToken.None);
        // Assert
        Assert.IsTrue(blockedSecondAction);
        Assert.AreEqual(2, stream.Sent.Count);
        Assert.AreNotEqual(sent.SubmitMove.ActionId, stream.Sent.Last().SubmitMove.ActionId);
    }

    [TestMethod]
    public async Task SubmitMoveAsync_DuplicateSnapshots_DoesNotResendWithinSameConnectionAsync()
    {
        // Arrange
        StubGameSessionStream stream = new StubGameSessionStream();
        await using GameplaySessionTestContext context = new GameplaySessionTestContext(stream);
        await context.StartAsync();
        await context.Session.SubmitMoveAsync(0, 1, CancellationToken.None);
        // Act
        stream.Publish(new GameSessionServerEvent { Snapshot = GameSessionTestData.Snapshot(2) });
        stream.Publish(new GameSessionServerEvent { Snapshot = GameSessionTestData.Snapshot(3) });
        await GameplaySessionTestContext.WaitUntilAsync(() => context.Updates.Any(update =>
            update.ServerEvent?.Snapshot?.Version == 3));
        // Assert
        Assert.AreEqual(1, stream.Sent.Count);
        Assert.IsTrue(context.Updates.Last().HasPendingAction);
    }

    [TestMethod]
    public async Task SendChatAsync_SendFails_DoesNotReplayChatAfterReconnectAsync()
    {
        // Arrange
        StubGameSessionStream first = new StubGameSessionStream { FailSend = true };
        StubGameSessionStream second = new StubGameSessionStream();
        await using GameplaySessionTestContext context = new GameplaySessionTestContext(
            first, second);
        await context.StartAsync();
        second.Publish(new GameSessionServerEvent { Snapshot = GameSessionTestData.Snapshot(2) });
        // Act
        bool sent = await context.Session.SendChatAsync("Hello", CancellationToken.None);
        await GameplaySessionTestContext.WaitUntilAsync(() => context.Updates.Any(update =>
            update.ServerEvent?.Snapshot?.Version == 2));
        // Assert
        Assert.IsFalse(sent);
        Assert.IsNotNull(first.Sent.Single().SendChatMessage);
        Assert.IsTrue(second.Sent.IsEmpty);
        Assert.IsTrue(context.Updates.Any(update => update.ChatSendFailed));
    }

    [TestMethod]
    [DataRow(StatusCode.Unauthenticated, ClientFailureReason.SessionExpired)]
    [DataRow(StatusCode.PermissionDenied, ClientFailureReason.PermissionDenied)]
    [DataRow(StatusCode.InvalidArgument, ClientFailureReason.InvalidRequest)]
    [DataRow(StatusCode.AlreadyExists, ClientFailureReason.ConnectionReplaced)]
    public async Task RunAsync_PermanentFailure_ReportsApplicationReasonAndStopsRetryingAsync(
        StatusCode statusCode, ClientFailureReason expected)
    {
        // Arrange
        StubGameSessionClient client = new StubGameSessionClient
        {
            ConnectFailure = new RpcException(new Status(statusCode, "Rejected."))
        };
        using GameplaySession session = new GameplaySession(client);
        GameplaySessionUpdate? last = null;
        session.Updated += update => last = update;
        // Act
        await session.RunAsync(1, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        // Assert
        Assert.AreEqual(1, client.ConnectCount);
        Assert.AreEqual(GameplayConnectionState.Closed, last!.State);
        Assert.AreEqual(expected, last.Failure);
    }

    [TestMethod]
    public async Task Dispose_ActiveConnection_StopsStreamAndRejectsLateUpdatesAsync()
    {
        // Arrange
        StubGameSessionStream stream = new StubGameSessionStream();
        await using GameplaySessionTestContext context = new GameplaySessionTestContext(stream);
        await context.StartAsync();
        int before = context.Updates.Count;
        // Act
        context.Session.Dispose();
        stream.Publish(new GameSessionServerEvent { Snapshot = GameSessionTestData.Snapshot(9) });
        await GameplaySessionTestContext.WaitUntilAsync(() => stream.IsDisposed);
        // Assert
        Assert.AreEqual(before, context.Updates.Count);
        Assert.IsTrue(stream.ReadCancellation.IsCancellationRequested);
        Assert.AreEqual(1, context.Client.ConnectCount);
    }

    [TestMethod]
    public async Task SubmitMoveAsync_AlreadyCancelled_DoesNotQueueAnActionAsync()
    {
        // Arrange
        StubGameSessionStream stream = new StubGameSessionStream();
        await using GameplaySessionTestContext context = new GameplaySessionTestContext(stream);
        await context.StartAsync();
        using CancellationTokenSource cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        // Act
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            context.Session.SubmitMoveAsync(0, 0, cancellation.Token));
        // Assert
        Assert.IsTrue(stream.Sent.IsEmpty);
        Assert.IsFalse(context.Updates.Last().HasPendingAction);
    }

    private static GameSessionServerEvent ActionResult(string actionId)
    {
        return new GameSessionServerEvent
        {
            ActionResult = new GameActionResponse { ActionId = actionId, IsAccepted = true }
        };
    }
}
