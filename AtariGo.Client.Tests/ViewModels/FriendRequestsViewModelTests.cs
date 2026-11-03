using System.Reflection;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

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
}
