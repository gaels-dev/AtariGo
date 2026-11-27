using AtariGo.Client.Services;
using AtariGo.Contracts;

namespace AtariGo.Client.Tests.Services;

[TestClass]
[TestCategory("Unit")]
public sealed class GameplaySessionClosingTests
{
    [TestMethod]
    public async Task CloseAsync_FinishedGame_CompletesBeforeCancellationAndDoesNotReconnectAsync()
    {
        StubGameSessionStream stream = new StubGameSessionStream();
        await using GameplaySessionTestContext context = new GameplaySessionTestContext(stream);
        await context.StartAsync();
        GameSessionSnapshot finished = GameSessionTestData.Snapshot(2);
        finished.Status = "Completed";
        finished.EndReason = "Surrender";
        finished.Winner = GameStoneState.White;
        stream.Publish(new GameSessionServerEvent { Snapshot = finished });
        await GameplaySessionTestContext.WaitUntilAsync(
            () => context.Updates.Last().ServerEvent?.Snapshot?.Status == "Completed");

        Task closing = context.Session.CloseAsync(CancellationToken.None);
        Task repeated = context.Session.CloseAsync(CancellationToken.None);
        await closing.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.AreSame(closing, repeated);
        Assert.IsTrue(stream.IsCompleting);
        Assert.IsFalse(stream.WasCancelledBeforeCompletion);
        Assert.IsFalse(stream.ReadCancellation.IsCancellationRequested);
        Assert.IsTrue(stream.IsDisposed);
        Assert.AreEqual(1, context.Client.ConnectCount);
    }

    [TestMethod]
    public async Task CloseAsync_ResponseStillOpen_WaitsForServerBeforeDisposingAsync()
    {
        StubGameSessionStream stream = new StubGameSessionStream { KeepResponseOpen = true };
        await using GameplaySessionTestContext context = new GameplaySessionTestContext(stream);
        await context.StartAsync();

        Task closing = context.Session.CloseAsync(CancellationToken.None);
        await GameplaySessionTestContext.WaitUntilAsync(() => stream.IsCompleting);
        bool pending = !closing.IsCompleted && !stream.IsDisposed;
        stream.FinishResponse();
        await closing.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.IsTrue(pending);
        Assert.IsTrue(stream.IsDisposed);
        Assert.IsFalse(stream.ReadCancellation.IsCancellationRequested);
    }

    [TestMethod]
    public async Task CloseAsync_CancelledWhileServerDoesNotRespond_AbortsAndDoesNotRetryAsync()
    {
        StubGameSessionStream stream = new StubGameSessionStream { KeepResponseOpen = true };
        await using GameplaySessionTestContext context = new GameplaySessionTestContext(stream);
        await context.StartAsync();
        using CancellationTokenSource cancellation = new CancellationTokenSource();

        Task closing = context.Session.CloseAsync(cancellation.Token);
        await GameplaySessionTestContext.WaitUntilAsync(() => stream.IsCompleting);
        cancellation.Cancel();
        await closing.WaitAsync(TimeSpan.FromSeconds(5));
        await GameplaySessionTestContext.WaitUntilAsync(() => stream.IsDisposed);

        Assert.IsTrue(stream.ReadCancellation.IsCancellationRequested);
        Assert.AreEqual(1, context.Client.ConnectCount);
    }

    [TestMethod]
    public async Task CloseAsync_ServerDoesNotFinish_AbortsWhenDeadlineExpiresAsync()
    {
        StubGameSessionStream stream = new StubGameSessionStream { KeepResponseOpen = true };
        await using GameplaySessionTestContext context = new GameplaySessionTestContext(stream);
        await context.StartAsync();

        await context.Session.CloseAsync(CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(10));
        await GameplaySessionTestContext.WaitUntilAsync(() => stream.IsDisposed);

        Assert.IsTrue(stream.IsCompleting);
        Assert.IsTrue(stream.ReadCancellation.IsCancellationRequested);
        Assert.AreEqual(1, context.Client.ConnectCount);
    }
}
