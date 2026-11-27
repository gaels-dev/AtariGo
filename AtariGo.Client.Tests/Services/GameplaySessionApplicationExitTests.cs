using AtariGo.Client.Services;
using AtariGo.Contracts;

namespace AtariGo.Client.Tests.Services;

[TestClass]
[TestCategory("Unit")]
public sealed class GameplaySessionApplicationExitTests
{
    [TestMethod]
    public async Task SurrenderAndCloseAsync_AcceptedResponse_ClosesAfterConfirmationAsync()
    {
        StubGameSessionStream stream = new StubGameSessionStream();
        await using GameplaySessionTestContext context = new GameplaySessionTestContext(stream);
        await context.StartAsync();

        Task exit = context.Session.SurrenderAndCloseAsync(CancellationToken.None);
        Task repeated = context.Session.SurrenderAndCloseAsync(CancellationToken.None);
        await WaitForSentCountAsync(stream, 1);
        bool waited = !exit.IsCompleted && !stream.IsDisposed && !stream.IsCompleting;
        stream.Publish(AcceptAction(stream.Sent.Single().Surrender.ActionId));
        await exit.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.AreSame(exit, repeated);
        Assert.IsTrue(waited);
        Assert.IsTrue(stream.IsCompleting);
        Assert.IsFalse(stream.WasCancelledBeforeCompletion);
        Assert.IsTrue(stream.IsDisposed);
        Assert.AreEqual(1, context.Client.ConnectCount);
    }

    [TestMethod]
    public async Task SurrenderAndCloseAsync_MovePending_WaitsBeforeSendingSurrenderAsync()
    {
        StubGameSessionStream stream = new StubGameSessionStream();
        await using GameplaySessionTestContext context = new GameplaySessionTestContext(stream);
        await context.StartAsync();
        await context.Session.SubmitMoveAsync(0, 1, CancellationToken.None);

        Task exit = context.Session.SurrenderAndCloseAsync(CancellationToken.None);
        await context.Session.SubmitMoveAsync(1, 1, CancellationToken.None);
        Assert.AreEqual(1, stream.Sent.Count);
        stream.Publish(AcceptAction(stream.Sent.Single().SubmitMove.ActionId));
        await WaitForSentCountAsync(stream, 2);
        GameSessionClientEvent surrender = stream.Sent.Last();
        stream.Publish(AcceptAction(surrender.Surrender.ActionId));
        await exit.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.IsNotNull(surrender.Surrender);
        Assert.IsTrue(stream.IsCompleting);
        Assert.AreEqual(2, stream.Sent.Count);
    }

    [TestMethod]
    public async Task SurrenderAndCloseAsync_NoResponse_AbortsWithoutInventingAResultAsync()
    {
        StubGameSessionStream stream = new StubGameSessionStream();
        await using GameplaySessionTestContext context = new GameplaySessionTestContext(stream);
        await context.StartAsync();

        await context.Session.SurrenderAndCloseAsync(CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(10));
        bool IsStreamDisposed()
        {
            return stream.IsDisposed;
        }
        await GameplaySessionTestContext.WaitUntilAsync(IsStreamDisposed);

        Assert.IsFalse(stream.IsCompleting);
        Assert.IsTrue(stream.ReadCancellation.IsCancellationRequested);
        Assert.AreEqual(1, stream.Sent.Count);
        foreach (AtariGo.Client.Models.GameplaySessionUpdate update in context.Updates)
        {
            if (update.ServerEvent?.Snapshot is GameSessionSnapshot snapshot)
            {
                Assert.AreNotEqual("Completed", snapshot.Status);
            }
        }
    }

    [TestMethod]
    public async Task
        SurrenderAndCloseAsync_RejectedResponse_DoesNotTreatRejectionAsAcceptanceAsync()
    {
        StubGameSessionStream stream = new StubGameSessionStream();
        await using GameplaySessionTestContext context = new GameplaySessionTestContext(stream);
        await context.StartAsync();
        using CancellationTokenSource cancellation = new CancellationTokenSource();
        Task exit = context.Session.SurrenderAndCloseAsync(cancellation.Token);
        await WaitForSentCountAsync(stream, 1);
        GameSessionServerEvent rejected = AcceptAction(stream.Sent.Single().Surrender.ActionId);
        rejected.ActionResult.IsAccepted = false;
        rejected.ActionResult.Rejection = "InvalidAction";

        stream.Publish(rejected);
        bool HasMatchingResult()
        {
            return context.Updates.Last().IsMatchingActionResult;
        }
        await GameplaySessionTestContext.WaitUntilAsync(HasMatchingResult);
        bool waited = !exit.IsCompleted && !stream.IsCompleting;
        cancellation.Cancel();
        await exit.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.IsTrue(waited);
        Assert.IsFalse(stream.IsCompleting);
    }

    private static Task WaitForSentCountAsync(StubGameSessionStream stream, int count)
    {
        bool HasExpectedCount()
        {
            return stream.Sent.Count == count;
        }
        return GameplaySessionTestContext.WaitUntilAsync(HasExpectedCount);
    }

    private static GameSessionServerEvent AcceptAction(string actionId)
    {
        return new GameSessionServerEvent
        {
            ActionResult = new GameActionResponse { ActionId = actionId, IsAccepted = true }
        };
    }
}
