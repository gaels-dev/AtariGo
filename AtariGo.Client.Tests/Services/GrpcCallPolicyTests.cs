using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using AtariGo.Client.Models;
using AtariGo.Client.Services;

namespace AtariGo.Client.Tests.Services
{
    [TestClass]
    public sealed class GrpcCallPolicyTests
    {
        private const int LeaderboardLimit = 10;

        [TestMethod]
        public async Task CreateOptions_AllCurrentRpcAdapters_ForwardDeadlineAndCancellation()
        {
            // Arrange
            using var connection = new GrpcClientConnection(new Uri("https://localhost:7061"));
            using var cancellation = new CancellationTokenSource();
            var recorder = new RecordingInterceptor();
            CallInvoker invoker = connection.CallInvoker.Intercept(recorder);
            var authentication = new GrpcAuthenticationClient(invoker);
            var registration = new GrpcRegistrationClient(invoker);
            var leaderboard = new GrpcLeaderboardClient(invoker);
            var session = new GrpcSessionClient("test-token", invoker);
            DateTime earliestDeadline = DateTime.UtcNow.AddSeconds(
                GrpcCallPolicy.RequestTimeoutSeconds);

            // Act
            await authentication.LoginAsync("player", "password", cancellation.Token);
            await registration.RegisterAccountAsync(new RegistrationDraft(), "text",
                cancellation.Token);
            await leaderboard.GetTopPlayersAsync(LeaderboardLimit, cancellation.Token);
            await session.HeartbeatAsync(cancellation.Token);
            await session.LogoutAsync(cancellation.Token);
            DateTime latestDeadline = DateTime.UtcNow.AddSeconds(
                GrpcCallPolicy.RequestTimeoutSeconds);

            // Assert
            CollectionAssert.AreEqual(
                new[] { "Login", "RegisterAccount", "GetTopPlayers", "Heartbeat", "Logout" },
                recorder.Calls.Select(call => call.MethodName).ToArray());
            foreach (RecordedGrpcCall call in recorder.Calls)
            {
                Assert.IsTrue(call.Options.Deadline >= earliestDeadline);
                Assert.IsTrue(call.Options.Deadline <= latestDeadline);
                Assert.AreEqual(cancellation.Token, call.Options.CancellationToken);
            }
        }

        [TestMethod]
        [DataRow(StatusCode.Cancelled, true, true)]
        [DataRow(StatusCode.Cancelled, false, false)]
        [DataRow(StatusCode.Unavailable, true, false)]
        [DataRow(StatusCode.DeadlineExceeded, false, false)]
        public void IsRequestedCancellation_RpcStatus_RequiresOwnerCancellation(
            StatusCode statusCode,
            bool cancelOwner,
            bool expected)
        {
            // Arrange
            using var cancellation = new CancellationTokenSource();
            if (cancelOwner)
            {
                cancellation.Cancel();
            }
            var exception = new RpcException(new Status(statusCode, "Test failure"));

            // Act
            bool actual = GrpcCallPolicy.IsRequestedCancellation(exception, cancellation.Token);

            // Assert
            Assert.AreEqual(expected, actual);
        }
    }
}
