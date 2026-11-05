using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using AtariGo.Client.Services;

namespace AtariGo.Client.Tests.Integration
{
    [TestClass]
    [TestCategory("Integration")]
    public sealed class GrpcTransportTests
    {
        private const int DynamicPort = 0;
        private const int TestCompletionSeconds = 5;

        [TestMethod]
        public async Task RunRealtimeAsync_CancellingOwner_StopsLongLivedStream()
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
                var session = new GrpcSessionClient(
                    "test-token",
                    connection.CallInvoker);
                var callbacks = new SessionRealtimeCallbacks(
                    _ => { },
                    _ => { },
                    () => { });
                Task streamTask = session.RunRealtimeAsync(
                    callbacks,
                    cancellation.Token);
                Task<TcpClient> acceptTask = listener.AcceptTcpClientAsync();

                using TcpClient acceptedClient = await acceptTask.WaitAsync(
                    TimeSpan.FromSeconds(TestCompletionSeconds));

                // Act
                cancellation.Cancel();
                await streamTask.WaitAsync(
                    TimeSpan.FromSeconds(TestCompletionSeconds));
            }
            finally
            {
                listener.Stop();
            }
        }
    }
}
