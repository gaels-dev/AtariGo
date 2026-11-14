using System.IO;

using Microsoft.EntityFrameworkCore;

using AtariGo.GameLogic.Models;
using AtariGo.Server.Application.Games;
using AtariGo.Server.Data;

namespace AtariGo.Server.Tests.Games;

[TestClass]
[TestCategory("Integration")]
public sealed class GameCoordinatorLifetimeTests
{
    private static readonly TimeSpan completionTimeout = TimeSpan.FromSeconds(5);

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task DisposeAsync_AdmittedOperations_DrainsBeforeClosing(bool cancelWaiter)
    {
        // Arrange
        GameActionLookupInterceptor interceptor = new GameActionLookupInterceptor();
        await using GameplayTestDatabase database = await GameplayTestDatabase.CreateAsync(
            interceptor);
        GameSessionConnection black = await database.ConnectAsync("Black");
        await database.ConnectAsync("White");
        GameActionCommand move = await database.CreateMoveAsync(new BoardPosition(0, 0));
        TaskCompletionSource entered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        interceptor.LookupAsyncCallback = () => PauseAsync(entered, release.Task);
        Task active = database.Coordinator.ProcessAsync(black, move, CancellationToken.None);
        await entered.Task.WaitAsync(completionTimeout);
        using CancellationTokenSource cancellation = new CancellationTokenSource();
        Task queued = database.Coordinator.AdvanceAsync(database.GameId, cancellation.Token);
        // Act
        database.Coordinator.Dispose();
        Task closing = database.Coordinator.DisposeAsync().AsTask();
        try
        {
            Assert.IsFalse(closing.IsCompleted);
            await Assert.ThrowsAsync<ObjectDisposedException>(() => database.Coordinator
                .AdvanceAsync(database.GameId, CancellationToken.None));
            if (cancelWaiter)
            {
                cancellation.Cancel();
                await Assert.ThrowsAsync<OperationCanceledException>(
                    () => queued.WaitAsync(completionTimeout));
            }
        }
        finally
        {
            release.TrySetResult();
        }
        await Task.WhenAll(active, closing).WaitAsync(completionTimeout);
        if (!cancelWaiter)
        {
            await queued.WaitAsync(completionTimeout);
        }
        // Assert
        await using AtariGoDbContext context = database.CreateContext();
        Assert.AreEqual(1, await context.Moves.CountAsync());
    }

    [TestMethod]
    public async Task DisposeAsync_FailingOperation_ReleasesItsGateBeforeClosing()
    {
        // Arrange
        GameActionLookupInterceptor interceptor = new GameActionLookupInterceptor();
        await using GameplayTestDatabase database = await GameplayTestDatabase.CreateAsync(
            interceptor);
        GameSessionConnection black = await database.ConnectAsync("Black");
        await database.ConnectAsync("White");
        GameActionCommand move = await database.CreateMoveAsync(new BoardPosition(0, 0));
        TaskCompletionSource entered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        interceptor.LookupAsyncCallback = () => FailLookupAsync(entered, release.Task);
        Task active = database.Coordinator.ProcessAsync(black, move, CancellationToken.None);
        await entered.Task.WaitAsync(completionTimeout);
        // Act
        Task closing = database.Coordinator.DisposeAsync().AsTask();
        release.TrySetResult();
        IOException failure = await Assert.ThrowsAsync<IOException>(
            () => active.WaitAsync(completionTimeout));
        await closing.WaitAsync(completionTimeout);
        // Assert
        Assert.AreEqual("Simulated read failure.", failure.Message);
    }

    private static Task PauseAsync(TaskCompletionSource entered, Task release)
    {
        entered.TrySetResult();
        return release;
    }

    private static async Task FailLookupAsync(TaskCompletionSource entered, Task release)
    {
        await PauseAsync(entered, release);
        throw new IOException("Simulated read failure.");
    }
}
