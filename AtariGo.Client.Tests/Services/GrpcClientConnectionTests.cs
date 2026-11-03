using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using AtariGo.Client.Services;

namespace AtariGo.Client.Tests.Services
{
    [TestClass]
    [TestCategory("Unit")]
    public sealed class GrpcClientConnectionTests
    {
        private const int LeaderboardLimit = 10;

        [TestMethod]
        public void Shared_RepeatedAccess_ReturnsSameConnectionAndInvoker()
        {
            // Arrange
            GrpcClientConnection firstConnection = GrpcClientConnection.Shared;

            // Act
            GrpcClientConnection secondConnection = GrpcClientConnection.Shared;

            // Assert
            Assert.AreSame(firstConnection, secondConnection);
            Assert.AreSame(firstConnection.CallInvoker, secondConnection.CallInvoker);
        }

        [TestMethod]
        public void Constructor_InsecureAddress_ThrowsArgumentException()
        {
            // Arrange
            var insecureAddress = new Uri("http://localhost:7061");

            // Act
            Action createConnection = () => new GrpcClientConnection(insecureAddress);

            // Assert
            Assert.ThrowsExactly<ArgumentException>(createConnection);
        }

        [TestMethod]
        public void Constructor_RelativeAddress_ThrowsArgumentException()
        {
            // Arrange
            var relativeAddress = new Uri(string.Empty, UriKind.Relative);

            // Act
            Action createConnection = () => new GrpcClientConnection(relativeAddress);

            // Assert
            Assert.ThrowsExactly<ArgumentException>(createConnection);
        }

        [TestMethod]
        public void Dispose_RepeatedCalls_RejectsFurtherRpcCalls()
        {
            // Arrange
            var connection = new GrpcClientConnection(new Uri("https://localhost:7061"));
            var leaderboard = new GrpcLeaderboardClient(connection.CallInvoker);

            // Act
            connection.Dispose();
            connection.Dispose();

            // Assert
            Assert.ThrowsExactly<ObjectDisposedException>(
                () => leaderboard.GetTopPlayersAsync(LeaderboardLimit));
        }
    }
}
