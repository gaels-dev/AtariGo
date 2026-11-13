using System.Reflection;


using Grpc.Core;

using AtariGo.Client.Services;
using AtariGo.Client.Tests.Services;
using AtariGo.Client.ViewModels;
using AtariGo.Contracts;

namespace AtariGo.Client.Tests.ViewModels;

[TestClass]
[TestCategory("Unit")]
public sealed class FriendRemovalViewModelTests
{
    [TestMethod]
    [DataRow(RemoveFriendshipStatus.Removed)]
    [DataRow(RemoveFriendshipStatus.AlreadyAbsent)]
    public async Task Remove_Confirmed_RemovesRealFriendAfterServerResponse(
        RemoveFriendshipStatus status)
    {
        // Arrange
        INavigationService navigation = DispatchProxy.Create<INavigationService,
            ConfirmationNavigationProxy>();
        var context = new FriendListNavigationContext();
        var friend = new FriendItemViewModel(2, "ActualFriend", true);
        context.Friends.Add(friend);
        var client = new StubFriendshipsClient
        {
            Response = Task.FromResult(new RemoveFriendshipResponse { Status = status })
        };
        using var viewModel = new FriendRemovalViewModel(navigation, context, client);

        // Act
        await CommandExecution.ExecuteAsync(viewModel.RemoveCommand, friend);

        // Assert
        Assert.HasCount(0, context.Friends);
        Assert.AreEqual(2, client.FriendUserId);
        StringAssert.Contains(((ConfirmationNavigationProxy)navigation).Message, "ActualFriend");
    }

    [TestMethod]
    public async Task Remove_Declined_PreservesFriendAndDoesNotCallServer()
    {
        // Arrange
        INavigationService navigation = DispatchProxy.Create<INavigationService,
            ConfirmationNavigationProxy>();
        ((ConfirmationNavigationProxy)navigation).Confirmed = false;
        var context = new FriendListNavigationContext();
        var friend = new FriendItemViewModel(2, "ActualFriend", true);
        context.Friends.Add(friend);
        var client = new StubFriendshipsClient();
        using var viewModel = new FriendRemovalViewModel(navigation, context, client);

        // Act
        await CommandExecution.ExecuteAsync(viewModel.RemoveCommand, friend);

        // Assert
        Assert.HasCount(1, context.Friends);
        Assert.AreEqual(0, client.Calls);
    }

    [TestMethod]
    public async Task Remove_ServerUnavailable_PreservesFriendAndReportsUnconfirmedResult()
    {
        // Arrange
        INavigationService navigation = DispatchProxy.Create<INavigationService,
            ConfirmationNavigationProxy>();
        var context = new FriendListNavigationContext();
        var friend = new FriendItemViewModel(2, "ActualFriend", true);
        context.Friends.Add(friend);
        var client = new StubFriendshipsClient
        {
            Response = Task.FromException<RemoveFriendshipResponse>(new RpcException(
                new Status(StatusCode.Unavailable, "Test outage")))
        };
        using var viewModel = new FriendRemovalViewModel(navigation, context, client);

        // Act
        await CommandExecution.ExecuteAsync(viewModel.RemoveCommand, friend);

        // Assert
        Assert.HasCount(1, context.Friends);
        Assert.IsFalse(string.IsNullOrEmpty(viewModel.StatusMessage));
    }

    [TestMethod]
    public async Task Dispose_PendingRemoval_CancelsAndIgnoresLateResponse()
    {
        // Arrange
        INavigationService navigation = DispatchProxy.Create<INavigationService,
            ConfirmationNavigationProxy>();
        var context = new FriendListNavigationContext();
        var friend = new FriendItemViewModel(2, "ActualFriend", true);
        context.Friends.Add(friend);
        var pending = new TaskCompletionSource<RemoveFriendshipResponse>();
        var client = new StubFriendshipsClient { Response = pending.Task };
        using var viewModel = new FriendRemovalViewModel(navigation, context, client);
        Task command = CommandExecution.ExecuteAsync(viewModel.RemoveCommand, friend);
        string previousStatus = viewModel.StatusMessage;

        // Act
        viewModel.Dispose();
        pending.SetResult(new RemoveFriendshipResponse { Status = RemoveFriendshipStatus.Removed });
        await command;

        // Assert
        Assert.IsTrue(client.RequestToken.IsCancellationRequested);
        Assert.HasCount(1, context.Friends);
        Assert.AreEqual(previousStatus, viewModel.StatusMessage);
    }
}
