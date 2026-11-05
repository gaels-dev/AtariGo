using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using AtariGo.Client.Services;

namespace AtariGo.Client.Tests.Services
{
    [TestClass]
    public sealed class GrpcSessionClientTests
    {
        private const int TestCompletionSeconds = 5;

        [TestMethod]
        public async Task RunRealtimeAsync_UsesSessionTokenOnStream()
        {
            // Arrange
            using var connection = new GrpcClientConnection(
                new Uri("https://localhost:7061"));
            var recorder = new RecordingInterceptor();
            CallInvoker invoker = connection.CallInvoker.Intercept(recorder);
            var session = new GrpcSessionClient("session-token", invoker);
            using var cancellation = new CancellationTokenSource();
            var callbacks = new SessionRealtimeCallbacks(
                _ => { },
                _ => cancellation.Cancel(),
                () => cancellation.Cancel());

            // Act
            await session.RunRealtimeAsync(callbacks, cancellation.Token)
                .WaitAsync(TimeSpan.FromSeconds(TestCompletionSeconds));

            // Assert
            RecordedGrpcCall call = recorder.Calls.Single();
            Assert.AreEqual("Connect", call.MethodName);
            Assert.AreEqual(
                "Bearer session-token",
                call.Headers.GetValue("authorization"));
        }

        [TestMethod]
        public async Task LogoutAsync_UsesOnlyItsOwnSessionToken()
        {
            // Arrange
            using var connection = new GrpcClientConnection(
                new Uri("https://localhost:7061"));
            var recorder = new RecordingInterceptor();
            CallInvoker invoker = connection.CallInvoker.Intercept(recorder);
            var firstSession = new GrpcSessionClient("first-token", invoker);
            var secondSession = new GrpcSessionClient("second-token", invoker);

            // Act
            await firstSession.LogoutAsync();
            await secondSession.LogoutAsync();

            // Assert
            CollectionAssert.AreEqual(
                new[] { "Logout", "Logout" },
                recorder.Calls.Select(call => call.MethodName).ToArray());
            CollectionAssert.AreEqual(
                new[] { "Bearer first-token", "Bearer second-token" },
                recorder.Calls.Select(call =>
                    call.Headers.GetValue("authorization") ?? string.Empty).ToArray());
        }
    }
}
