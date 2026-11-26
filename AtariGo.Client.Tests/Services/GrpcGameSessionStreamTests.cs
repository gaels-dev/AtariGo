using System.IO;

using AtariGo.Client.Services;
using AtariGo.Contracts;

namespace AtariGo.Client.Tests.Services;

[TestClass]
[TestCategory("Unit")]
public sealed class GrpcGameSessionStreamTests
{
    private static readonly TimeSpan completionTimeout = TimeSpan.FromSeconds(5);

    [TestMethod]
    public async Task CompleteAsync_ActiveAndQueuedWrites_CompletesOnceAfterAllWritesAsync()
    {
        ControlledGameSessionTransport transport = new ControlledGameSessionTransport();
        await using GrpcGameSessionStream stream = new GrpcGameSessionStream(transport.CreateCall());
        Task active = stream.SendEventAsync(new GameSessionClientEvent(), CancellationToken.None);
        await transport.FirstWriteStarted.WaitAsync(completionTimeout);
        Task queued = stream.SendEventAsync(new GameSessionClientEvent(), CancellationToken.None);

        Task completing = stream.CompleteAsync(CancellationToken.None);
        Task repeated = stream.CompleteAsync(CancellationToken.None);
        bool waited = !completing.IsCompleted && transport.CompleteCount == 0;
        await Assert.ThrowsAsync<InvalidOperationException>(() => stream.SendEventAsync(
            new GameSessionClientEvent(), CancellationToken.None));
        transport.CompleteFirstWrite();
        await Task.WhenAll(active, queued, completing).WaitAsync(completionTimeout);

        Assert.IsTrue(waited);
        Assert.AreSame(completing, repeated);
        Assert.AreEqual(2, transport.WriteCount);
        Assert.AreEqual(1, transport.CompleteCount);
        Assert.AreEqual(0, transport.DisposeCount);
    }

    [TestMethod]
    public async Task CompleteAsync_CancelledWithPendingWrite_AllowsForcedDisposalAsync()
    {
        ControlledGameSessionTransport transport = new ControlledGameSessionTransport();
        GrpcGameSessionStream stream = new GrpcGameSessionStream(transport.CreateCall());
        Task active = stream.SendEventAsync(new GameSessionClientEvent(), CancellationToken.None);
        await transport.FirstWriteStarted.WaitAsync(completionTimeout);
        using CancellationTokenSource cancellation = new CancellationTokenSource();

        Task completing = stream.CompleteAsync(cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => completing);
        Task disposal = stream.DisposeAsync().AsTask();
        transport.CompleteFirstWrite();
        await Task.WhenAll(active, disposal).WaitAsync(completionTimeout);

        Assert.AreEqual(0, transport.CompleteCount);
        Assert.AreEqual(1, transport.DisposeCount);
    }

    [TestMethod]
    public async Task DisposeAsync_ActiveAndQueuedSends_WaitsAndRejectsFurtherWritesAsync()
    {
        // Arrange
        ControlledGameSessionTransport transport = new ControlledGameSessionTransport();
        GrpcGameSessionStream stream = new GrpcGameSessionStream(transport.CreateCall());
        Task active = stream.SendEventAsync(new GameSessionClientEvent(), CancellationToken.None);
        await transport.FirstWriteStarted.WaitAsync(completionTimeout);
        Task queued = stream.SendEventAsync(new GameSessionClientEvent(), CancellationToken.None);
        // Act
        Task closing = stream.DisposeAsync().AsTask();
        try
        {
            Assert.IsFalse(closing.IsCompleted);
            await Assert.ThrowsAsync<ObjectDisposedException>(() => stream.SendEventAsync(
                new GameSessionClientEvent(), CancellationToken.None));
        }
        finally
        {
            transport.CompleteFirstWrite();
        }
        await active.WaitAsync(completionTimeout);
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => queued.WaitAsync(completionTimeout));
        await closing.WaitAsync(completionTimeout);
        await stream.DisposeAsync();
        // Assert
        Assert.AreEqual(1, transport.WriteCount);
        Assert.AreEqual(1, transport.DisposeCount);
    }

    [TestMethod]
    public async Task DisposeAsync_CancelledWaiter_DoesNotDelayClosingAsync()
    {
        // Arrange
        ControlledGameSessionTransport transport = new ControlledGameSessionTransport();
        GrpcGameSessionStream stream = new GrpcGameSessionStream(transport.CreateCall());
        Task active = stream.SendEventAsync(new GameSessionClientEvent(), CancellationToken.None);
        await transport.FirstWriteStarted.WaitAsync(completionTimeout);
        using CancellationTokenSource cancellation = new CancellationTokenSource();
        Task queued = stream.SendEventAsync(new GameSessionClientEvent(), cancellation.Token);
        // Act
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => queued.WaitAsync(completionTimeout));
        Task closing = stream.DisposeAsync().AsTask();
        transport.CompleteFirstWrite();
        await Task.WhenAll(active, closing).WaitAsync(completionTimeout);
        // Assert
        Assert.AreEqual(1, transport.WriteCount);
        Assert.AreEqual(1, transport.DisposeCount);
    }

    [TestMethod]
    public async Task DisposeAsync_FailingActiveWrite_PreservesTransportFailureAsync()
    {
        // Arrange
        ControlledGameSessionTransport transport = new ControlledGameSessionTransport
        {
            FirstWriteFailure = new IOException("The connection was closed.")
        };
        GrpcGameSessionStream stream = new GrpcGameSessionStream(transport.CreateCall());
        Task active = stream.SendEventAsync(new GameSessionClientEvent(), CancellationToken.None);
        await transport.FirstWriteStarted.WaitAsync(completionTimeout);
        // Act
        Task closing = stream.DisposeAsync().AsTask();
        transport.CompleteFirstWrite();
        IOException failure = await Assert.ThrowsAsync<IOException>(
            () => active.WaitAsync(completionTimeout));
        await closing.WaitAsync(completionTimeout);
        // Assert
        Assert.AreSame(transport.FirstWriteFailure, failure);
        Assert.AreEqual(1, transport.DisposeCount);
    }

    [TestMethod]
    public async Task SendEventAsync_ConcurrentWrites_SerializesMessagesAsync()
    {
        // Arrange
        ControlledGameSessionTransport transport = new ControlledGameSessionTransport();
        await using GrpcGameSessionStream stream = new GrpcGameSessionStream(
            transport.CreateCall());
        Task first = stream.SendEventAsync(new GameSessionClientEvent(), CancellationToken.None);
        await transport.FirstWriteStarted.WaitAsync(completionTimeout);
        // Act
        Task second = stream.SendEventAsync(new GameSessionClientEvent(), CancellationToken.None);
        try
        {
            Assert.AreEqual(1, transport.WriteCount);
            Assert.IsFalse(second.IsCompleted);
        }
        finally
        {
            transport.CompleteFirstWrite();
        }
        await Task.WhenAll(first, second).WaitAsync(completionTimeout);
        // Assert
        Assert.AreEqual(2, transport.WriteCount);
    }
}
