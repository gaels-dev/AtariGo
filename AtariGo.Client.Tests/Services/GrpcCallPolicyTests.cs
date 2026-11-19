using Grpc.Core;
using Grpc.Core.Interceptors;

using AtariGo.Client.Models;
using AtariGo.Client.Services;
using AtariGo.Contracts;

namespace AtariGo.Client.Tests.Services
{
    [TestClass]
    [TestCategory("Unit")]
    public sealed class GrpcCallPolicyTests
    {
        private const int LeaderboardLimit = 10;

        [TestMethod]
        [DataRow(StatusCode.Unauthenticated, ClientFailureReason.SessionExpired)]
        [DataRow(StatusCode.Unavailable, ClientFailureReason.Unavailable)]
        [DataRow(StatusCode.NotFound, ClientFailureReason.NotFound)]
        public async Task ExecuteAsync_TransportFailure_ExposesApplicationReasonAsync(
            StatusCode status, ClientFailureReason expected)
        {
            // Arrange
            var transport = new RpcException(new Status(status, "BusinessCode"));

            // Act
            ClientRequestException failure =
                await Assert.ThrowsExactlyAsync<ClientRequestException>(
                    () => GrpcCallPolicy.ExecuteAsync(() => Task.FromException<int>(transport)));

            // Assert
            Assert.AreEqual(expected, failure.Reason);
            Assert.AreEqual("BusinessCode", failure.ErrorCode);
            Assert.AreSame(transport, failure.InnerException);
            Assert.IsFalse(failure.Message.Contains("BusinessCode", StringComparison.Ordinal));
        }

        [TestMethod]
        public async Task ExecuteAsync_SuspensionTrailer_PreservesSpecificReasonAsync()
        {
            // Arrange
            var trailers = new Metadata
            {
                { GrpcFailureCodes.TrailerKey, GrpcFailureCodes.AccountSuspended }
            };
            var transport = new RpcException(
                new Status(StatusCode.PermissionDenied, "The account is suspended."), trailers);

            // Act
            ClientRequestException failure =
                await Assert.ThrowsExactlyAsync<ClientRequestException>(
                    () => GrpcCallPolicy.ExecuteAsync(() => Task.FromException<int>(transport)));

            // Assert
            Assert.AreEqual(ClientFailureReason.PermissionDenied, failure.Reason);
            Assert.AreEqual(GrpcFailureCodes.AccountSuspended, failure.ErrorCode);
            Assert.AreSame(transport, failure.InnerException);
        }

        [TestMethod]
        public async Task RpcAdapters_UnaryAndStreamingCalls_ApplyLifetimePolicyAsync()
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
            await registration.RegisterAccountAsync(new RegistrationDraft(),
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
