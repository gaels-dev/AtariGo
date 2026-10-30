using System;
using System.Linq;
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
        [TestMethod]
        public async Task HeartbeatAsync_SharedTransport_KeepsSessionTokensSeparate()
        {
            // Arrange
            using var connection = new GrpcClientConnection(new Uri("https://localhost:7061"));
            var recorder = new RecordingInterceptor();
            CallInvoker invoker = connection.CallInvoker.Intercept(recorder);
            var firstSession = new GrpcSessionClient("first-token", invoker);
            var secondSession = new GrpcSessionClient("second-token", invoker);

            // Act
            await firstSession.HeartbeatAsync();
            await secondSession.HeartbeatAsync();

            // Assert
            string[] authorizationHeaders = recorder.Calls
                .Select(call => call.Headers.GetValue("authorization") ?? string.Empty)
                .ToArray();
            CollectionAssert.AreEqual(
                new[] { "Bearer first-token", "Bearer second-token" },
                authorizationHeaders);
        }

        [TestMethod]
        public async Task LogoutAsync_SharedTransport_DoesNotCloseAnotherSession()
        {
            // Arrange
            using var connection = new GrpcClientConnection(new Uri("https://localhost:7061"));
            var recorder = new RecordingInterceptor();
            CallInvoker invoker = connection.CallInvoker.Intercept(recorder);
            var firstSession = new GrpcSessionClient("first-token", invoker);
            var secondSession = new GrpcSessionClient("second-token", invoker);

            // Act
            await firstSession.LogoutAsync();
            await secondSession.HeartbeatAsync();

            // Assert
            CollectionAssert.AreEqual(
                new[] { "Logout", "Heartbeat" },
                recorder.Calls.Select(call => call.MethodName).ToArray());
            Assert.AreEqual(
                "Bearer second-token",
                recorder.Calls.Last().Headers.GetValue("authorization"));
        }

        [TestMethod]
        public async Task LoginAsync_AfterAuthenticatedCall_DoesNotReuseSessionHeader()
        {
            // Arrange
            using var connection = new GrpcClientConnection(new Uri("https://localhost:7061"));
            var recorder = new RecordingInterceptor();
            CallInvoker invoker = connection.CallInvoker.Intercept(recorder);
            var session = new GrpcSessionClient("private-token", invoker);
            var authentication = new GrpcAuthenticationClient(invoker);

            // Act
            await session.HeartbeatAsync();
            await authentication.LoginAsync("player", "password");

            // Assert
            Assert.IsNull(recorder.Calls.Last().Headers.GetValue("authorization"));
        }
    }
}
