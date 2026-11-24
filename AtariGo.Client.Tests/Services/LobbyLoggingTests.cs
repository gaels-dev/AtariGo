using System.Text.Json;

using Grpc.Core.Interceptors;
using Grpc.Core;

using AtariGo.Client.Services;
using AtariGo.Contracts;

namespace AtariGo.Client.Tests.Services;

[TestClass]
[TestCategory("Integration")]
[DoNotParallelize]
[OSCondition(OperatingSystems.Windows)]
public sealed class LobbyLoggingTests
{
    private readonly TestContext _testContext;

    public LobbyLoggingTests(TestContext testContext)
    {
        _testContext = testContext;
    }

    [TestMethod]
    [DataRow(StatusCode.Cancelled, true, "Cancelled")]
    [DataRow(StatusCode.Cancelled, false, "Unconfirmed")]
    [DataRow(StatusCode.DeadlineExceeded, true, "Unconfirmed")]
    [DataRow(StatusCode.PermissionDenied, false, "Rejected")]
    public async Task Send_TransportFailure_RecordsSafeCauseAndPreservesResultAsync(
        StatusCode status, bool cancel, string outcome)
    {
        // Arrange
        using var directory = new ClientDiagnosticTestDirectory();
        await using var diagnostics = new ClientDiagnostics(directory.Options);
        using var connection = new GrpcClientConnection(new Uri("https://localhost:7061"));
        var interceptor = new LobbyResponseInterceptor
        {
            Response = Task.FromException<SendRoomInvitationResponse>(new RpcException(
                new Status(status, "private-detail"), new Metadata { { "private", "secret" } }))
        };
        var client = new GrpcRoomInvitationsClient("private-token",
            connection.CallInvoker.Intercept(interceptor));
        using var cancellation = new CancellationTokenSource();
        if (cancel)
        {
            await cancellation.CancelAsync();
        }
        // Act
        SendRoomInvitationResponse response = await client.SendAsync(
            new SendRoomInvitationRequest { RoomId = 1, RecipientUserId = 2 }, cancellation.Token);
        await diagnostics.FlushAsync(_testContext.CancellationToken);
        JsonElement entry = directory.ReadEvents().Single(item =>
            item.GetProperty("Operation").GetString() == "SendInvitationRequest");
        // Assert
        Assert.AreEqual(RoomInvitationDeliveryStatus.Unconfirmed, response.DeliveryStatus);
        Assert.AreEqual(outcome, entry.GetProperty("Outcome").GetString());
        Assert.AreEqual((int)status, entry.GetProperty("Error")
            .GetProperty("NativeCode").GetInt32());
        Assert.IsFalse(entry.GetRawText().Contains("private", StringComparison.Ordinal));
        Assert.IsFalse(entry.GetRawText().Contains("secret", StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow(RoomInvitationDeliveryStatus.InApp, "Succeeded")]
    [DataRow(RoomInvitationDeliveryStatus.EmailUnconfirmed, "Unconfirmed")]
    [DataRow(RoomInvitationDeliveryStatus.RateLimited, "Rejected")]
    public async Task Send_ConfirmedResponse_DistinguishesDeliveryFromRequestCompletionAsync(
        RoomInvitationDeliveryStatus status, string outcome)
    {
        // Arrange
        using var directory = new ClientDiagnosticTestDirectory();
        await using var diagnostics = new ClientDiagnostics(directory.Options);
        using var connection = new GrpcClientConnection(new Uri("https://localhost:7061"));
        var interceptor = new LobbyResponseInterceptor
        {
            Response = Task.FromResult(new SendRoomInvitationResponse
            {
                DeliveryStatus = status,
                Invitation = new RoomInvitation
                {
                    RoomCode = "private-code",
                    SenderName = "private"
                }
            })
        };
        var client = new GrpcRoomInvitationsClient("private-token",
            connection.CallInvoker.Intercept(interceptor));
        // Act
        SendRoomInvitationResponse response = await client.SendAsync(
            new SendRoomInvitationRequest { RoomId = 1, RecipientUserId = 2 },
            _testContext.CancellationToken);
        await diagnostics.FlushAsync(_testContext.CancellationToken);
        JsonElement entry = directory.ReadEvents().Single(item =>
            item.GetProperty("Operation").GetString() == "SendInvitationRequest");
        // Assert
        Assert.AreEqual(status, response.DeliveryStatus);
        Assert.AreEqual(outcome, entry.GetProperty("Outcome").GetString());
        Assert.AreEqual(status.ToString(), entry.GetProperty("Status")
            .GetProperty("Value").GetString());
        Assert.IsFalse(entry.GetRawText().Contains("private", StringComparison.Ordinal));
    }
    [TestMethod]
    public async Task Search_RepeatedUpdates_DoNotDuplicateLogsOrExposeOpponentAsync()
    {
        // Arrange
        using var directory = new ClientDiagnosticTestDirectory();
        await using var diagnostics = new ClientDiagnostics(directory.Options);
        using var connection = new GrpcClientConnection(new Uri("https://localhost:7061"));
        var queued = new MatchmakingEvent { SearchQueued = new SearchQueued() };
        var paired = new MatchmakingEvent
        {
            MatchCountdownStarted = new MatchCountdownStarted { OpponentName = "private-opponent" }
        };
        var cancelled = new MatchmakingEvent
        {
            MatchCountdownCancelled = new MatchCountdownCancelled()
        };
        var interceptor = new LobbyStreamInterceptor
        {
            Reader = new LobbyStreamReader(
                [queued, queued, paired, paired, cancelled, queued, paired])
        };
        var client = new GrpcMatchmakingClient(null, "private-guest",
            connection.CallInvoker.Intercept(interceptor));
        int received = 0;
        // Act
        await foreach (MatchmakingEvent update in client.SearchPublicMatchAsync(
            _testContext.CancellationToken))
        {
            received++;
        }
        await diagnostics.FlushAsync(_testContext.CancellationToken);
        JsonElement[] entries = directory.ReadEvents().Where(item =>
            item.GetProperty("Operation").GetString() == "SearchRequest").ToArray();
        // Assert
        Assert.AreEqual(7, received);
        Assert.HasCount(5, entries);
        Assert.AreEqual("Cancelled", entries[2].GetProperty("Outcome").GetString());
        Assert.DoesNotContain(entry => entry.GetRawText().Contains("private",
            StringComparison.Ordinal), entries);
    }
}
