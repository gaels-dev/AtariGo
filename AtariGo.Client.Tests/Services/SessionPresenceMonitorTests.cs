using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Grpc.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using AtariGo.Client.Services;
using AtariGo.Contracts;

namespace AtariGo.Client.Tests.Services
{
    [TestClass]
    public sealed class SessionPresenceMonitorTests
    {
        private const int TestPollingMilliseconds = 1;
        private const int TestCompletionSeconds = 5;
        private const int ExpectedRecoveryCalls = 2;

        [TestMethod]
        public async Task RunAsync_TransientNetworkFailure_ReceivesNextSuccessfulPresence()
        {
            // Arrange
            using var cancellation = new CancellationTokenSource();
            var events = new List<string>();
            int calls = 0;
            var client = new StubSessionClient(_ => ++calls == 1
                ? Task.FromException<HeartbeatResponse>(
                    new RpcException(new Status(StatusCode.Unavailable, "Network unavailable")))
                : Task.FromResult(new HeartbeatResponse()));
            var monitor = new SessionPresenceMonitor(client,
                TimeSpan.FromMilliseconds(TestPollingMilliseconds));
            monitor.RequestFailed += _ => events.Add("unavailable");
            monitor.PresenceReceived += _ =>
            {
                events.Add("received");
                cancellation.Cancel();
            };

            // Act
            await monitor.RunAsync(cancellation.Token).WaitAsync(
                TimeSpan.FromSeconds(TestCompletionSeconds));

            // Assert
            Assert.AreEqual(ExpectedRecoveryCalls, calls);
            CollectionAssert.AreEqual(new[] { "unavailable", "received" }, events);
        }

        [TestMethod]
        public async Task RunAsync_InvalidSession_StopsAndRaisesSessionExpired()
        {
            // Arrange
            bool expired = false;
            bool networkFailure = false;
            var client = new StubSessionClient(_ => Task.FromException<HeartbeatResponse>(
                new RpcException(new Status(StatusCode.Unauthenticated, "Invalid session"))));
            var monitor = new SessionPresenceMonitor(client,
                TimeSpan.FromMilliseconds(TestPollingMilliseconds));
            monitor.SessionExpired += () => expired = true;
            monitor.RequestFailed += _ => networkFailure = true;

            // Act
            await monitor.RunAsync(CancellationToken.None).WaitAsync(
                TimeSpan.FromSeconds(TestCompletionSeconds));

            // Assert
            Assert.IsTrue(expired);
            Assert.IsFalse(networkFailure);
        }

        [TestMethod]
        public async Task RunAsync_OwnerCancelsRpc_DoesNotReportConnectionFailure()
        {
            // Arrange
            using var cancellation = new CancellationTokenSource();
            bool failed = false;
            var client = new StubSessionClient(_ =>
            {
                cancellation.Cancel();
                return Task.FromException<HeartbeatResponse>(
                    new RpcException(new Status(StatusCode.Cancelled, "Owner cancelled")));
            });
            var monitor = new SessionPresenceMonitor(client,
                TimeSpan.FromMilliseconds(TestPollingMilliseconds));
            monitor.RequestFailed += _ => failed = true;

            // Act
            await monitor.RunAsync(cancellation.Token);

            // Assert
            Assert.IsFalse(failed);
        }

        [TestMethod]
        public async Task RunAsync_ResponseArrivesAfterCancellation_DiscardsPresence()
        {
            // Arrange
            using var cancellation = new CancellationTokenSource();
            bool received = false;
            var client = new StubSessionClient(_ =>
            {
                cancellation.Cancel();
                return Task.FromResult(new HeartbeatResponse());
            });
            var monitor = new SessionPresenceMonitor(client,
                TimeSpan.FromMilliseconds(TestPollingMilliseconds));
            monitor.PresenceReceived += _ => received = true;

            // Act
            await monitor.RunAsync(cancellation.Token);

            // Assert
            Assert.IsFalse(received);
        }
    }
}
