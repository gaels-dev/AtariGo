using System.Text.Json;

using Grpc.Core;
using Grpc.Core.Interceptors;

using AtariGo.Client.Models;
using AtariGo.Client.Services;
using AtariGo.Contracts;

namespace AtariGo.Client.Tests.Services;

[TestClass]
[TestCategory("Integration")]
[DoNotParallelize]
[OSCondition(OperatingSystems.Windows)]
public sealed class PlayerLoggingTests
{
    private readonly TestContext _testContext;

    public PlayerLoggingTests(TestContext testContext)
    {
        _testContext = testContext;
    }

    [TestMethod]
    [DataRow(StatusCode.Cancelled, true, "Cancelled")]
    [DataRow(StatusCode.Cancelled, false, "Unconfirmed")]
    [DataRow(StatusCode.DeadlineExceeded, true, "Unconfirmed")]
    [DataRow(StatusCode.PermissionDenied, false, "Rejected")]
    public async Task Report_TransportFailure_DoesNotConfuseCancellationOrLoseSafeCauseAsync(
        StatusCode status, bool cancel, string expectedOutcome)
    {
        // Arrange
        using var directory = new ClientDiagnosticTestDirectory();
        await using var diagnostics = new ClientDiagnostics(directory.Options);
        using var connection = new GrpcClientConnection(new Uri("https://localhost:7061"));
        var response = new PlayerResponseInterceptor<SubmitReportResponse>
        {
            Response = Task.FromException<SubmitReportResponse>(new RpcException(
                new Status(status, "private-failure")))
        };
        var client = new GrpcReportsClient("private-token",
            connection.CallInvoker.Intercept(response));
        using var cancellation = new CancellationTokenSource();
        if (cancel)
        {
            await cancellation.CancelAsync();
        }
        // Act
        PlayerReportResult result = await client.SubmitReportAsync(
            new PlayerReportDraft(1, PlayerReportCategory.Chat, "private-description"),
            cancellation.Token);
        await diagnostics.FlushAsync(_testContext.CancellationToken);
        JsonElement entry = directory.ReadEvents().Single(item =>
            ReadOperation(item) == "SubmitReport");
        // Assert
        Assert.AreNotEqual(ClientFailureReason.None, result.Failure);
        Assert.AreEqual(expectedOutcome, entry.GetProperty("Outcome").GetString());
        Assert.AreEqual((int)status, entry.GetProperty("Error")
            .GetProperty("NativeCode").GetInt32());
        Assert.IsFalse(entry.GetRawText().Contains("private", StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow(ProfileUpdateStatus.Updated, "Succeeded")]
    [DataRow(ProfileUpdateStatus.UserNameTaken, "Rejected")]
    [DataRow(ProfileUpdateStatus.Unspecified, "Unconfirmed")]
    [DataRow((ProfileUpdateStatus)999, "Unconfirmed")]
    public async Task Profile_Result_RecordsOnlyKnownStatusAndNeverPayloadAsync(
        ProfileUpdateStatus status, string outcome)
    {
        // Arrange
        using var directory = new ClientDiagnosticTestDirectory();
        await using var diagnostics = new ClientDiagnostics(directory.Options);
        using var connection = new GrpcClientConnection(new Uri("https://localhost:7061"));
        var response = new PlayerResponseInterceptor<UpdateProfileResponse>
        {
            Response = Task.FromResult(new UpdateProfileResponse
            {
                Status = status,
                Profile = new UserProfile
                {
                    UserName = "private-name",
                    Email = "private@example.invalid"
                }
            })
        };
        var client = new GrpcProfileClient("private-token",
            connection.CallInvoker.Intercept(response));
        // Act
        UpdateProfileResponse result = await client.UpdateProfileAsync(new UpdateProfileRequest
        {
            UserName = "private-new",
            AvatarId = 1
        }, _testContext.CancellationToken);
        await diagnostics.FlushAsync(_testContext.CancellationToken);
        JsonElement entry = directory.ReadEvents().Single(item =>
            ReadOperation(item) == "UpdateProfile");
        // Assert
        Assert.AreEqual(status, result.Status);
        Assert.AreEqual(outcome, entry.GetProperty("Outcome").GetString());
        Assert.IsFalse(entry.GetRawText().Contains("private", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task History_EmptyAndMalformedResponses_DistinguishesSuccessFromUncertaintyAsync()
    {
        // Arrange
        using var directory = new ClientDiagnosticTestDirectory();
        await using var diagnostics = new ClientDiagnostics(directory.Options);
        using var connection = new GrpcClientConnection(new Uri("https://localhost:7061"));
        var response = new GameHistoryResponseInterceptor();
        var client = new GrpcGameHistoryClient("private-token",
            connection.CallInvoker.Intercept(response));
        // Act
        GameHistoryQueryResult empty = await client.ListGamesByPlayerAsync(
            _testContext.CancellationToken);
        var malformed = new ListGamesByPlayerResponse();
        malformed.Entries.Add(new GameHistorySummary { OpponentName = "private-name", GameId = 1 });
        response.Response = Task.FromResult(malformed);
        GameHistoryQueryResult invalid = await client.ListGamesByPlayerAsync(
            _testContext.CancellationToken);
        await diagnostics.FlushAsync(_testContext.CancellationToken);
        JsonElement[] entries = directory.ReadEvents().Where(item =>
            ReadOperation(item) == "QueryHistory").ToArray();
        // Assert
        Assert.AreEqual(ClientFailureReason.None, empty.Failure);
        Assert.AreEqual(ClientFailureReason.RequestFailed, invalid.Failure);
        Assert.HasCount(2, entries);
        Assert.AreEqual("Succeeded", entries[0].GetProperty("Outcome").GetString());
        Assert.AreEqual("Unconfirmed", entries[1].GetProperty("Outcome").GetString());
        Assert.DoesNotContain(entry => entry.GetRawText().Contains("private",
            StringComparison.Ordinal),
            entries);
        var accepted = new PlayerResponseInterceptor<SubmitReportResponse>
        {
            Response = Task.FromResult(new SubmitReportResponse
            {
                Result = ReportResult.Accepted,
                ReportId = 1
            })
        };
        var reports = new GrpcReportsClient("private-token",
            connection.CallInvoker.Intercept(accepted));
        PlayerReportResult result = await reports.SubmitReportAsync(
            new PlayerReportDraft(1, PlayerReportCategory.Chat, "private-description"),
            _testContext.CancellationToken);
        Assert.AreEqual(ClientFailureReason.None, result.Failure);
        Assert.AreEqual(PlayerReportStatus.Accepted, result.Status);
    }

    [TestMethod]
    public async Task Profile_LostResponse_RecordsWrappedTransportCauseOnceAsync()
    {
        // Arrange
        using var directory = new ClientDiagnosticTestDirectory();
        await using var diagnostics = new ClientDiagnostics(directory.Options);
        using var connection = new GrpcClientConnection(new Uri("https://localhost:7061"));
        var response = new PlayerResponseInterceptor<GetProfileResponse>
        {
            Response = Task.FromException<GetProfileResponse>(new RpcException(
                new Status(StatusCode.Unavailable, "private-failure")))
        };
        var client = new GrpcProfileClient("private-token",
            connection.CallInvoker.Intercept(response));
        // Act
        await Assert.ThrowsAsync<ClientRequestException>(() =>
            client.GetProfileAsync(_testContext.CancellationToken));
        await diagnostics.FlushAsync(_testContext.CancellationToken);
        JsonElement entry = directory.ReadEvents().Single(item =>
            ReadOperation(item) == "ReadProfile");
        // Assert
        Assert.AreEqual("Unconfirmed", entry.GetProperty("Outcome").GetString());
        Assert.AreEqual((int)StatusCode.Unavailable,
            entry.GetProperty("Error").GetProperty("NativeCode").GetInt32());
        Assert.IsFalse(entry.GetRawText().Contains("private", StringComparison.Ordinal));
    }

    private static string ReadOperation(JsonElement entry)
    {
        return entry.GetProperty("Operation").GetString()!;
    }
}
