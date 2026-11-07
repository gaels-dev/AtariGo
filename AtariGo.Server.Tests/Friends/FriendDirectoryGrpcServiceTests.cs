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

    private static CallOptions Options(string token)
    {
        return new CallOptions(new Metadata { { "authorization", $"Bearer {token}" } },
            DateTime.UtcNow.AddSeconds(TestTimeoutSeconds));
    }
}
