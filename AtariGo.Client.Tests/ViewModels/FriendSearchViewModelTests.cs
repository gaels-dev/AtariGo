using System.Globalization;
using System.Reflection;

using Grpc.Core;

using AtariGo.Client.Properties;
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
    public async Task SearchCommand_ValidQuery_RefreshesResultsAndTrimsQueryAsync()
    {
        // Arrange
        INavigationService navigation = DispatchProxy.Create<INavigationService,
            RecordingNavigationProxy>();
        SearchUsersResponse response = new SearchUsersResponse();
        response.Users.Add(new FriendAccount { UserId = 2, UserName = "PlayerTwo" });
        StubFriendClient client = new StubFriendClient
        {
            SearchResponse = Task.FromResult(response)
        };
        using FriendSearchViewModel viewModel = new FriendSearchViewModel(navigation,
            "token", client);
        viewModel.SearchText = "  Player  ";

        // Act
        await CommandExecution.ExecuteAsync(viewModel.SearchCommand, null);
        FriendSearchResultViewModel initialResult = viewModel.SearchResults.Single();
        client.SearchResponse = Task.FromResult(new SearchUsersResponse());
        viewModel.SearchText = "Other";
        await CommandExecution.ExecuteAsync(viewModel.SearchCommand, null);

        // Assert
        CollectionAssert.AreEqual(new[] { "Player", "Other" }, client.SearchQueries);
        Assert.AreEqual(2, initialResult.UserId);
        Assert.AreEqual("PlayerTwo", initialResult.UserName);
        Assert.AreEqual("Other", client.LastSearchQuery);
        Assert.HasCount(0, viewModel.SearchResults);
        Assert.AreEqual(Localize("FriendSearch_Msg_NoResults"), viewModel.StatusMessage);
    }

    [TestMethod]
    [DataRow(" ", "FriendSearch_Err_MinLength")]
    [DataRow("1234567890123456", "FriendSearch_Err_InvalidLength")]
    public async Task SearchCommand_InvalidQuery_DoesNotCallServiceAsync(
        string query, string messageKey)
    {
        // Arrange
        INavigationService navigation = DispatchProxy.Create<INavigationService,
            RecordingNavigationProxy>();
        StubFriendClient client = new StubFriendClient();
        using FriendSearchViewModel viewModel = new FriendSearchViewModel(navigation,
            "token", client);
        viewModel.SearchText = query;

        // Act
        await CommandExecution.ExecuteAsync(viewModel.SearchCommand, null);

        // Assert
        Assert.IsNull(client.LastSearchQuery);
        Assert.AreEqual(Localize(messageKey), viewModel.StatusMessage);
    }

    [TestMethod]
    [DataRow(FriendRequestStatus.Sent, "FriendSearch_Msg_RequestSent")]
    [DataRow(FriendRequestStatus.AlreadyFriends, "FriendSearch_Msg_AlreadyFriends")]
    public async Task AddFriendCommand_ConfirmedStatus_UpdatesPlayerActionAsync(
        FriendRequestStatus status, string messageKey)
    {
        // Arrange
        INavigationService navigation = DispatchProxy.Create<INavigationService,
            RecordingNavigationProxy>();
        SearchUsersResponse searchResponse = new SearchUsersResponse();
        searchResponse.Users.Add(new FriendAccount { UserId = 2, UserName = "PlayerTwo" });
        StubFriendClient client = new StubFriendClient
        {
            SearchResponse = Task.FromResult(searchResponse),
            SendResponse = Task.FromResult(new SendFriendRequestResponse { Status = status })
        };
        using FriendSearchViewModel viewModel = new FriendSearchViewModel(navigation,
            "token", client);
        viewModel.SearchText = "PlayerTwo";
        await CommandExecution.ExecuteAsync(viewModel.SearchCommand, null);
        FriendSearchResultViewModel player = viewModel.SearchResults.Single();

        // Act
        await CommandExecution.ExecuteAsync(viewModel.AddFriendCommand, player);

        // Assert
        Assert.AreEqual(2, client.LastRecipientUserId);
        Assert.IsTrue(player.IsRequestSent);
        Assert.IsFalse(player.CanSendRequest);
        Assert.AreEqual(Localize(messageKey), viewModel.StatusMessage);
    }

    [TestMethod]
    public async Task SearchCommand_ExpiredSession_ShowsLocalizedErrorAsync()
    {
        // Arrange
        INavigationService navigation = DispatchProxy.Create<INavigationService,
            RecordingNavigationProxy>();
        RpcException failure = new RpcException(new Status(StatusCode.Unauthenticated,
            "Session expired."));
        StubFriendClient client = new StubFriendClient
        {
            SearchResponse = Task.FromException<SearchUsersResponse>(failure)
        };
        using FriendSearchViewModel viewModel = new FriendSearchViewModel(navigation,
            "token", client);
        viewModel.SearchText = "Player";

        // Act
        await CommandExecution.ExecuteAsync(viewModel.SearchCommand, null);

        // Assert
        Assert.HasCount(0, viewModel.SearchResults);
        Assert.AreEqual(Localize("FriendSearch_Err_SessionExpired"),
            viewModel.StatusMessage);
    }

    [TestMethod]
    public async Task Dispose_PendingSearch_CancelsRequestAndIgnoresLateResultsAsync()
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

    private static string Localize(string key)
    {
        return Resources.ResourceManager.GetString(key, CultureInfo.CurrentUICulture)
            ?? key;
    }
}
