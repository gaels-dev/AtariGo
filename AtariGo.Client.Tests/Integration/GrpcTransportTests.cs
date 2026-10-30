using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using Grpc.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using AtariGo.Client.Services;
using AtariGo.Contracts;

namespace AtariGo.Client.Tests.Integration
{
    [TestClass]
    [TestCategory("Integration")]
    public sealed class GrpcTransportTests
    {
        private const int DynamicPort = 0;
        private const int TestCompletionSeconds = 5;

        [TestMethod]
        public async Task HeartbeatAsync_ServerAcceptsButNeverResponds_ExceedsDeadline()
        {
            // Arrange
            var listener = new TcpListener(IPAddress.Loopback, DynamicPort);
            listener.Start();
            try
            {
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                using var connection = new GrpcClientConnection(
                    new Uri($"https://127.0.0.1:{port}"));
                var session = new GrpcSessionClient("test-token", connection.CallInvoker);
                Task<TcpClient> acceptTask = listener.AcceptTcpClientAsync();

                // Act
                Task<HeartbeatResponse> request = session.HeartbeatAsync();
                using TcpClient acceptedClient = await acceptTask.WaitAsync(
                    TimeSpan.FromSeconds(TestCompletionSeconds));
                RpcException exception = await Assert.ThrowsExactlyAsync<RpcException>(
                    async () => await request.WaitAsync(TimeSpan.FromSeconds(
                        GrpcCallPolicy.RequestTimeoutSeconds + TestCompletionSeconds)));

                // Assert
                Assert.AreEqual(StatusCode.DeadlineExceeded, exception.StatusCode);
            }
            finally
            {
                listener.Stop();
            }
        }

        [TestMethod]
        public async Task HeartbeatAsync_OwnerCancelsPendingConnection_StopsBeforeDeadline()
        {
            // Arrange
            using var cancellation = new CancellationTokenSource();
            var listener = new TcpListener(IPAddress.Loopback, DynamicPort);
            listener.Start();
            try
            {
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                using var connection = new GrpcClientConnection(
                    new Uri($"https://127.0.0.1:{port}"));
                var session = new GrpcSessionClient("test-token", connection.CallInvoker);
                Task<TcpClient> acceptTask = listener.AcceptTcpClientAsync();
                Task<HeartbeatResponse> request = session.HeartbeatAsync(cancellation.Token);
                using TcpClient acceptedClient = await acceptTask.WaitAsync(
                    TimeSpan.FromSeconds(TestCompletionSeconds));

                // Act
                cancellation.Cancel();
                RpcException exception = await Assert.ThrowsExactlyAsync<RpcException>(
                    async () => await request.WaitAsync(
                        TimeSpan.FromSeconds(TestCompletionSeconds)));

                // Assert
                Assert.AreEqual(StatusCode.Cancelled, exception.StatusCode);
            }
            finally
            {
                listener.Stop();
            }
        }
    }
}
