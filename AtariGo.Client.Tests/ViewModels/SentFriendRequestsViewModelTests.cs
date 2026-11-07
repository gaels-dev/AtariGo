using Microsoft.VisualStudio.TestTools.UnitTesting;

using Grpc.Core;

using AtariGo.Client.Tests.Services;
using AtariGo.Client.ViewModels;
using AtariGo.Contracts;

namespace AtariGo.Client.Tests.ViewModels;

[TestClass]
[TestCategory("Unit")]
public sealed class SentFriendRequestsViewModelTests
{
    [TestMethod]
    public async Task LoadMore_TwoPages_AppendsRequestsAndRefreshStartsAgain()
    {
        // Arrange
        var client = new StubFriendClient();
        client.SentPages.Enqueue(CreatePage(1, "next"));
        client.SentPages.Enqueue(CreatePage(2, string.Empty));
        client.SentPages.Enqueue(CreatePage(3, string.Empty));
        using var viewModel = new SentFriendRequestsViewModel(client);

        // Act
        await viewModel.LoadAsync();
        bool hadNextPage = viewModel.CanLoadMore;
        await CommandExecution.ExecuteAsync(viewModel.LoadMoreCommand, null);
        int loadedCount = viewModel.Requests.Count;
        await viewModel.LoadAsync();

        // Assert
        Assert.IsTrue(hadNextPage);
        Assert.AreEqual(2, loadedCount);
        Assert.HasCount(1, viewModel.Requests);
        Assert.AreEqual(3L, viewModel.Requests[0].RequestId);
        Assert.IsFalse(viewModel.CanLoadMore);
        CollectionAssert.AreEqual(new[] { "", "next", "" }, client.PageTokens);
    }

    [TestMethod]
    [DataRow(CancelFriendRequestStatus.Cancelled)]
    [DataRow(CancelFriendRequestStatus.NoLongerPending)]
    public async Task Cancel_ConfirmedOrStale_RemovesPendingRow(CancelFriendRequestStatus status)
    {
        // Arrange
        var client = new StubFriendClient
        {
            SentResponse = Task.FromResult(CreatePage(1, string.Empty)),
            CancelStatus = status
        };
        using var viewModel = new SentFriendRequestsViewModel(client);
        await viewModel.LoadAsync();

        // Act
        await CommandExecution.ExecuteAsync(viewModel.Requests[0].CancelCommand, null);

        // Assert
        Assert.HasCount(0, viewModel.Requests);
        Assert.IsFalse(string.IsNullOrEmpty(viewModel.StatusMessage));
    }

    [TestMethod]
    public async Task Cancel_ConnectionFailure_PreservesPendingRow()
    {
        // Arrange
        var client = new StubFriendClient
        {
            SentResponse = Task.FromResult(CreatePage(1, string.Empty)),
            CancelResponse = Task.FromException<CancelFriendRequestResponse>(new RpcException(
                new Status(StatusCode.Unavailable, "Test outage")))
        };
        using var viewModel = new SentFriendRequestsViewModel(client);
        await viewModel.LoadAsync();

        // Act
        await CommandExecution.ExecuteAsync(viewModel.Requests[0].CancelCommand, null);

        // Assert
        Assert.HasCount(1, viewModel.Requests);
        Assert.IsFalse(string.IsNullOrEmpty(viewModel.StatusMessage));
    }

    [TestMethod]
    public async Task Dispose_PendingLoad_DoesNotApplyLateResponse()
    {
        // Arrange
        var pending = new TaskCompletionSource<ListFriendRequestsBySenderResponse>();
        var client = new StubFriendClient { SentResponse = pending.Task };
        using var viewModel = new SentFriendRequestsViewModel(client);
        Task loading = viewModel.LoadAsync();
        string previousStatus = viewModel.StatusMessage;

        // Act
        viewModel.Dispose();
        pending.SetResult(CreatePage(1, string.Empty));
        await loading;

        // Assert
        Assert.IsTrue(client.RequestToken.IsCancellationRequested);
        Assert.HasCount(0, viewModel.Requests);
        Assert.AreEqual(previousStatus, viewModel.StatusMessage);
    }

    [TestMethod]
    public async Task Dispose_PendingCancellation_IgnoresLateResponse()
    {
        // Arrange
        var pending = new TaskCompletionSource<CancelFriendRequestResponse>();
        var client = new StubFriendClient
        {
            SentResponse = Task.FromResult(CreatePage(1, string.Empty)),
            CancelResponse = pending.Task
        };
        using var viewModel = new SentFriendRequestsViewModel(client);
        await viewModel.LoadAsync();
        Task command = CommandExecution.ExecuteAsync(viewModel.Requests[0].CancelCommand, null);
        string previousStatus = viewModel.StatusMessage;

        // Act
        viewModel.Dispose();
        pending.SetResult(new CancelFriendRequestResponse
        {
            Status = CancelFriendRequestStatus.Cancelled
        });
        await command;

        // Assert
        Assert.IsTrue(client.RequestToken.IsCancellationRequested);
        Assert.HasCount(1, viewModel.Requests);
        Assert.AreEqual(previousStatus, viewModel.StatusMessage);
    }

    private static ListFriendRequestsBySenderResponse CreatePage(long requestId, string nextToken)
    {
        var page = new ListFriendRequestsBySenderResponse { NextPageToken = nextToken };
        page.Requests.Add(new OutgoingFriendRequest
        {
            RequestId = requestId, RecipientUserId = 2, RecipientUserName = "Player"
        });
        return page;
    }
}
