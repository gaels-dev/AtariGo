using System.Reflection;


using AtariGo.Client.Services;
using AtariGo.Client.Tests.Services;
using AtariGo.Client.ViewModels;
using AtariGo.Contracts;

namespace AtariGo.Client.Tests.ViewModels;

[TestClass]
[TestCategory("Unit")]
public sealed class FriendRequestsViewModelTests
{
    [TestMethod]
    public async Task Dispose_PendingInitialLoad_CancelsRequestAndIgnoresLateResults()
    {
        // Arrange
        INavigationService navigation = DispatchProxy.Create<INavigationService,
            RecordingNavigationProxy>();
        var pending = new TaskCompletionSource<GetIncomingFriendRequestsResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new StubFriendClient { IncomingResponse = pending.Task };
        using var viewModel = new FriendRequestsViewModel(navigation,
            new FriendRequestsContext { SessionToken = "token" }, client);
        string previousStatus = viewModel.StatusMessage;
        var response = new GetIncomingFriendRequestsResponse();
        response.Requests.Add(new IncomingFriendRequest
        {
            RequestId = 3,
            RequesterUserId = 2,
            RequesterUserName = "player"
        });

        // Act
        viewModel.Dispose();
        pending.SetResult(response);
        await viewModel.Initialization;

        // Assert
        Assert.IsTrue(client.RequestToken.IsCancellationRequested);
        Assert.AreEqual(0, viewModel.Requests.Count);
        Assert.AreEqual(previousStatus, viewModel.StatusMessage);
    }

    [TestMethod]
    public async Task Accept_ResponseAfterRemoval_DoesNotReinsertFriend()
    {
        // Arrange
        INavigationService navigation = DispatchProxy.Create<INavigationService,
            RecordingNavigationProxy>();
        var incoming = new GetIncomingFriendRequestsResponse();
        incoming.Requests.Add(new IncomingFriendRequest
        {
            RequestId = 1, RequesterUserId = 2, RequesterUserName = "Player"
        });
        var pending = new TaskCompletionSource<RespondToFriendRequestResponse>();
        var client = new StubFriendClient
        {
            IncomingResponse = Task.FromResult(incoming), Response = pending.Task
        };
        var context = new FriendRequestsContext();
        using var viewModel = new FriendRequestsViewModel(navigation, context, client);
        await viewModel.Initialization;
        Task response = CommandExecution.ExecuteAsync(viewModel.Requests[0].AcceptCommand, null);

        // Act
        context.Friends.Add(new FriendItemViewModel(2, "Player", true));
        context.Friends.Clear();
        pending.SetResult(new RespondToFriendRequestResponse
        {
            Status = FriendRequestResponseStatus.Accepted
        });
        await response;

        // Assert
        Assert.HasCount(0, context.Friends);
        Assert.HasCount(0, viewModel.Requests);
    }
}
