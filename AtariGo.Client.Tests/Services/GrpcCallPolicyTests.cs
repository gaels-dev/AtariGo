

using Grpc.Core;
using Grpc.Core.Interceptors;

using AtariGo.Client.Models;
using AtariGo.Client.Services;

namespace AtariGo.Client.Tests.Services
{
    [TestClass]
    [TestCategory("Unit")]
    public sealed class GrpcCallPolicyTests
    {
        private const int LeaderboardLimit = 10;

        [TestMethod]
        public async Task RpcAdapters_ApplyDeadlinesAndStreamingCancellation()
        {
            // Arrange
            using var connection = new GrpcClientConnection(new Uri("https://localhost:7061"));
            using var cancellation = new CancellationTokenSource();
            using var streamCancellation = new CancellationTokenSource();
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
            var callbacks = new SessionRealtimeCallbacks(
                _ => { },
                _ => streamCancellation.Cancel(),
                () => streamCancellation.Cancel());
            await session.RunRealtimeAsync(callbacks, streamCancellation.Token);
            await session.LogoutAsync(cancellation.Token);
            DateTime latestDeadline = DateTime.UtcNow.AddSeconds(
                GrpcCallPolicy.RequestTimeoutSeconds);

            // Assert
            CollectionAssert.AreEqual(
                new[] { "Login", "RegisterAccount", "GetTopPlayers", "Connect", "Logout" },
                recorder.Calls.Select(call => call.MethodName).ToArray());
            foreach (RecordedGrpcCall call in recorder.Calls.Where(
                         call => call.MethodName != "Connect"))
            {
                Assert.IsTrue(call.Options.Deadline >= earliestDeadline);
                Assert.IsTrue(call.Options.Deadline <= latestDeadline);
                Assert.AreEqual(cancellation.Token, call.Options.CancellationToken);
            }

            RecordedGrpcCall streamCall = recorder.Calls.Single(
                call => call.MethodName == "Connect");
            Assert.IsNull(streamCall.Options.Deadline);
            Assert.AreEqual(streamCancellation.Token, streamCall.Options.CancellationToken);
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
