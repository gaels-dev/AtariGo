using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Grpc.Core;
using Grpc.Net.Client;

using AtariGo.Contracts;

namespace AtariGo.Server.Tests.Friends;

[TestClass]
[TestCategory("Integration")]
public sealed class FriendDirectoryGrpcServiceTests
{
    private const int TestTimeoutSeconds = 10;

    [TestMethod]
    public async Task SentRequests_AuthenticatedSender_ListsAndCancelsOnlyOwnedRequests()
    {
        // Arrange
        await using SocialGrpcTestHost server = await SocialGrpcTestHost.StartAsync();
        using GrpcChannel channel = GrpcChannel.ForAddress(server.Address);
        var client = new FriendDirectory.FriendDirectoryClient(channel);
        CallOptions host = Options("host");
        await client.SendFriendRequestAsync(new SendFriendRequestRequest
        {
            RecipientUserId = 2
        }, host);
        await client.SendFriendRequestAsync(new SendFriendRequestRequest
        {
            RecipientUserId = 3
        }, host);
        ListFriendRequestsBySenderResponse sent = await client.ListFriendRequestsBySenderAsync(
            new ListFriendRequestsBySenderRequest
            {
                Page = new PageRequest { PageSize = 1 }
            }, host);
        ListFriendRequestsBySenderResponse next = await client.ListFriendRequestsBySenderAsync(
            new ListFriendRequestsBySenderRequest
            {
                Page = new PageRequest { PageToken = sent.NextPageToken }
            }, host);
        long requestId = sent.Requests.Single().RequestId;

        // Act
        CancelFriendRequestResponse denied = await client.CancelFriendRequestAsync(
            new CancelFriendRequestRequest { RequestId = requestId }, Options("other"));
        GetIncomingFriendRequestsResponse received = await client.GetIncomingFriendRequestsAsync(
            new GetIncomingFriendRequestsRequest(), Options("guest"));
        CancelFriendRequestResponse cancelled = await client.CancelFriendRequestAsync(
            new CancelFriendRequestRequest { RequestId = requestId }, host);
        GetIncomingFriendRequestsResponse after = await client.GetIncomingFriendRequestsAsync(
            new GetIncomingFriendRequestsRequest(), Options("guest"));

        // Assert
        Assert.IsFalse(string.IsNullOrEmpty(sent.NextPageToken));
        Assert.AreEqual(3, next.Requests.Single().RecipientUserId);
        Assert.IsTrue(string.IsNullOrEmpty(next.NextPageToken));
        Assert.AreEqual(CancelFriendRequestStatus.NoLongerPending, denied.Status);
        Assert.HasCount(1, received.Requests);
        Assert.AreEqual(CancelFriendRequestStatus.Cancelled, cancelled.Status);
        Assert.HasCount(0, after.Requests);
    }

    [TestMethod]
    public async Task ListSentRequests_InvalidPageOrSession_ReturnsControlledGrpcStatus()
    {
        // Arrange
        await using SocialGrpcTestHost server = await SocialGrpcTestHost.StartAsync();
        using GrpcChannel channel = GrpcChannel.ForAddress(server.Address);
        var client = new FriendDirectory.FriendDirectoryClient(channel);

        // Act
        RpcException invalidPage = await Assert.ThrowsAsync<RpcException>(() =>
            client.ListFriendRequestsBySenderAsync(new ListFriendRequestsBySenderRequest
            {
                Page = new PageRequest { PageToken = "invalid!" }
            }, Options("host")).ResponseAsync);
        RpcException invalidSession = await Assert.ThrowsAsync<RpcException>(() =>
            client.ListFriendRequestsBySenderAsync(new ListFriendRequestsBySenderRequest(),
                Options("invalid")).ResponseAsync);
        RpcException missingToken = await Assert.ThrowsAsync<RpcException>(() =>
            client.CancelFriendRequestAsync(new CancelFriendRequestRequest { RequestId = 1 })
                .ResponseAsync);

        // Assert
        Assert.AreEqual(StatusCode.InvalidArgument, invalidPage.StatusCode);
        Assert.AreEqual(StatusCode.Unauthenticated, invalidSession.StatusCode);
        Assert.AreEqual(StatusCode.Unauthenticated, missingToken.StatusCode);
    }

    [TestMethod]
    [DataRow(0L)]
    [DataRow(-1L)]
    public async Task RespondToFriendRequest_InvalidIdentifier_ReturnsInvalidArgument(
        long requestId)
    {
        // Arrange
        await using SocialGrpcTestHost server = await SocialGrpcTestHost.StartAsync();
        using GrpcChannel channel = GrpcChannel.ForAddress(server.Address);
        var client = new FriendDirectory.FriendDirectoryClient(channel);
        var request = new RespondToFriendRequestRequest { RequestId = requestId, Accept = true };

        // Act
        RpcException failure = await Assert.ThrowsExactlyAsync<RpcException>(() =>
            client.RespondToFriendRequestAsync(request, Options("guest")).ResponseAsync);

        // Assert
        Assert.AreEqual(StatusCode.InvalidArgument, failure.StatusCode);
        Assert.AreEqual("The friend request identifier must be positive.", failure.Status.Detail);
    }

    [TestMethod]
    [DataRow("search", "read", StatusCode.Internal)]
    [DataRow("search", "transientRead", StatusCode.Unavailable)]
    [DataRow("send", "save", StatusCode.Internal)]
    [DataRow("send", "transientSave", StatusCode.Unavailable)]
    [DataRow("respond", "conflict", StatusCode.Aborted)]
    [DataRow("incoming", "read", StatusCode.Internal)]
    [DataRow("sent", "read", StatusCode.Internal)]
    [DataRow("cancel", "read", StatusCode.Internal)]
    [DataRow("search", "argument", StatusCode.Unknown)]
    [DataRow("respond", "range", StatusCode.Unknown)]
    [DataRow("search", "query", StatusCode.InvalidArgument)]
    public async Task FriendOperation_Failure_ReturnsSafeStatusWithoutRepeating(
        string operationName, string failureKind, StatusCode expectedStatus)
    {
        // Arrange
        var service = new FailingFriendService(() => FailAsync(failureKind));
        await using SocialGrpcTestHost server = await SocialGrpcTestHost.StartAsync(service);
        using GrpcChannel channel = GrpcChannel.ForAddress(server.Address);
        var client = new FriendDirectory.FriendDirectoryClient(channel);

        // Act
        RpcException failure = await Assert.ThrowsExactlyAsync<RpcException>(() =>
            InvokeOperationAsync(client, operationName));

        // Assert
        Assert.AreEqual(expectedStatus, failure.StatusCode);
        Assert.IsFalse(failure.Status.Detail.Contains("Private", StringComparison.Ordinal));
        Assert.AreEqual(1, service.InvocationCount);
    }

    [TestMethod]
    [DataRow(StatusCode.Cancelled)]
    [DataRow(StatusCode.PermissionDenied)]
    public async Task FriendOperation_GrpcFailure_PreservesOriginalStatus(StatusCode status)
    {
        // Arrange
        var service = new FailingFriendService(() =>
            throw new RpcException(new Status(status, "Controlled failure.")));
        await using SocialGrpcTestHost server = await SocialGrpcTestHost.StartAsync(service);
        using GrpcChannel channel = GrpcChannel.ForAddress(server.Address);
        var client = new FriendDirectory.FriendDirectoryClient(channel);

        // Act
        RpcException failure = await Assert.ThrowsExactlyAsync<RpcException>(() =>
            InvokeOperationAsync(client, "search"));

        // Assert
        Assert.AreEqual(status, failure.StatusCode);
        Assert.AreEqual("Controlled failure.", failure.Status.Detail);
        Assert.AreEqual(1, service.InvocationCount);
    }

    private static Task FailAsync(string failureKind)
    {
        switch (failureKind)
        {
            case "read":
                throw new FriendTestDatabaseException(false);
            case "transientRead":
                throw new FriendTestDatabaseException(true);
            case "save":
                throw new DbUpdateException("Private persistence detail.");
            case "transientSave":
                throw new DbUpdateException("Private persistence detail.",
                    new FriendTestDatabaseException(true));
            case "conflict":
                throw new DbUpdateConcurrencyException("Private concurrency detail.");
            case "argument":
                throw new ArgumentException("Private dependency detail.", "configuration");
            case "range":
                throw new ArgumentOutOfRangeException(
                    "configuration", "Private dependency detail.");
            case "query":
                throw new ArgumentException("Private argument detail.", "query");
            default:
                throw new ArgumentOutOfRangeException(nameof(failureKind));
        }
    }

    private static Task InvokeOperationAsync(
        FriendDirectory.FriendDirectoryClient client, string operationName)
    {
        CallOptions options = Options("host");
        return operationName switch
        {
            "search" => client.SearchUsersAsync(new SearchUsersRequest { Query = "Al" },
                options).ResponseAsync,
            "send" => client.SendFriendRequestAsync(new SendFriendRequestRequest
                { RecipientUserId = 2 }, options).ResponseAsync,
            "respond" => client.RespondToFriendRequestAsync(new RespondToFriendRequestRequest
                { RequestId = 1, Accept = true }, options).ResponseAsync,
            "incoming" => client.GetIncomingFriendRequestsAsync(
                new GetIncomingFriendRequestsRequest(), options).ResponseAsync,
            "sent" => client.ListFriendRequestsBySenderAsync(
                new ListFriendRequestsBySenderRequest(), options).ResponseAsync,
            "cancel" => client.CancelFriendRequestAsync(new CancelFriendRequestRequest
                { RequestId = 1 }, options).ResponseAsync,
            _ => throw new ArgumentOutOfRangeException(nameof(operationName))
        };
    }

    private static CallOptions Options(string token)
    {
        return new CallOptions(new Metadata { { "authorization", $"Bearer {token}" } },
            DateTime.UtcNow.AddSeconds(TestTimeoutSeconds));
    }
}
