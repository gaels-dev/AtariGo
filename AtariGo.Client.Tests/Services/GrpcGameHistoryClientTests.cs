using Grpc.Core;
using Grpc.Core.Interceptors;

using AtariGo.Client.Models;
using AtariGo.Client.Services;
using AtariGo.Contracts;

namespace AtariGo.Client.Tests.Services;

[TestClass]
[TestCategory("Unit")]
public sealed class GrpcGameHistoryClientTests
{
    [TestMethod]
    public async Task ListGamesByPlayer_TransportAndInvalidSummary_ReturnsControlledFailuresAsync()
    {
        // Arrange
        using var connection = new GrpcClientConnection(new Uri("https://localhost:7061"));
        var interceptor = new GameHistoryResponseInterceptor();
        var client = new GrpcGameHistoryClient("owner",
            connection.CallInvoker.Intercept(interceptor));
        using var cancellation = new CancellationTokenSource();
        DateTime earliest = DateTime.UtcNow;
        // Act
        GameHistoryQueryResult empty = await client.ListGamesByPlayerAsync(cancellation.Token);
        var validResponse = new ListGamesByPlayerResponse();
        validResponse.Entries.Add(new GameHistorySummary
        {
            GameId = 2,
            PlayedAtUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            OpponentName = "Guest-Rival",
            Result = "Won",
            State = "Completed",
            StoneColor = "Black",
            OpponentStoneColor = "White"
        });
        interceptor.Response = Task.FromResult(validResponse);
        GameHistoryQueryResult valid = await client.ListGamesByPlayerAsync(cancellation.Token);
        interceptor.Response = Task.FromException<ListGamesByPlayerResponse>(
            new RpcException(new Status(StatusCode.Unauthenticated, "Private details")));
        GameHistoryQueryResult unauthorized = await client.ListGamesByPlayerAsync(
            cancellation.Token);
        var response = new ListGamesByPlayerResponse();
        response.Entries.Add(new GameHistorySummary { GameId = 1, OpponentName = "Incomplete" });
        interceptor.Response = Task.FromResult(response);
        GameHistoryQueryResult invalid = await client.ListGamesByPlayerAsync(cancellation.Token);
        // Assert
        Assert.AreEqual(ClientFailureReason.None, empty.Failure);
        Assert.HasCount(0, empty.Entries);
        Assert.AreEqual(ClientFailureReason.None, valid.Failure);
        Assert.AreEqual("Guest-Rival", valid.Entries.Single().OpponentName);
        Assert.AreEqual(ClientFailureReason.SessionExpired, unauthorized.Failure);
        Assert.AreEqual(ClientFailureReason.RequestFailed, invalid.Failure);
        Assert.HasCount(0, invalid.Entries);
        Assert.AreEqual("Bearer owner", interceptor.Options.Headers?.GetValue("authorization"));
        Assert.AreEqual(cancellation.Token, interceptor.Options.CancellationToken);
        Assert.IsTrue(interceptor.Options.Deadline > earliest);
    }
}
