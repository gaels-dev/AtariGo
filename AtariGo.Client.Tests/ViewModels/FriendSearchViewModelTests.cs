using System.Reflection;


using AtariGo.Client.Services;
using AtariGo.Client.Tests.Services;
using AtariGo.Client.ViewModels;
using AtariGo.Contracts;

namespace AtariGo.Client.Tests.ViewModels;

[TestClass]
[TestCategory("Unit")]
public sealed class FriendSearchViewModelTests
{
    [TestMethod]
    public async Task Dispose_PendingSearch_CancelsRequestAndIgnoresLateResults()
    {
        // Arrange
        INavigationService navigation = DispatchProxy.Create<INavigationService,
            RecordingNavigationProxy>();
        var pending = new TaskCompletionSource<SearchUsersResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new StubFriendClient { SearchResponse = pending.Task };
        using var viewModel = new FriendSearchViewModel(navigation, "token", client);
        viewModel.SearchText = "player";
        Task command = CommandExecution.ExecuteAsync(viewModel.SearchCommand, null);
        string previousStatus = viewModel.StatusMessage;
        var response = new SearchUsersResponse();
        response.Users.Add(new FriendAccount { UserId = 2, UserName = "player" });

        // Act
        viewModel.Dispose();
        pending.SetResult(response);
        await command;

        // Assert
        Assert.IsTrue(client.RequestToken.IsCancellationRequested);
        Assert.AreEqual(0, viewModel.SearchResults.Count);
        Assert.AreEqual(previousStatus, viewModel.StatusMessage);
    }
}
