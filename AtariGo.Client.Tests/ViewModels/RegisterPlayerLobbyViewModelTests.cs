using System.Reflection;

using Grpc.Core.Interceptors;

using AtariGo.Client.Services;
using AtariGo.Client.Tests.Services;
using AtariGo.Client.ViewModels;

namespace AtariGo.Client.Tests.ViewModels;

[TestClass]
[TestCategory("Unit")]
public sealed class RegisterPlayerLobbyViewModelTests
{
    [TestMethod]
    public void InviteFriendByEmailCommand_OfflineFriend_OpensCreationWithRecipient()
    {
        // Arrange
        INavigationService navigation =
            DispatchProxy.Create<INavigationService, RecordingNavigationProxy>();
        var context = new RegisteredLobbyContext();
        using var connection = new GrpcClientConnection(new Uri("https://localhost:7061"));
        var leaderboard = new GrpcLeaderboardClient(
            connection.CallInvoker.Intercept(new RecordingInterceptor()));
        var friend = new FriendItemViewModel(7, "Friend", false);
        context.Friends.Add(friend);
        using var model = new RegisterPlayerLobbyViewModel(
            navigation, leaderboard, context);

        // Act
        model.InviteFriendByEmailCommand.Execute(friend);
        friend.IsOnline = true;
        model.InviteFriendByEmailCommand.Execute(friend);
        model.InviteFriendByEmailCommand.Execute(new FriendItemViewModel(8, "Other", false));

        // Assert
        RecordingNavigationProxy recorder = (RecordingNavigationProxy)navigation;
        CollectionAssert.AreEqual(new[]
        {
            nameof(INavigationService.NavigateToCreatePrivateRoomForInvitation)
        }, recorder.Calls.ToArray());
        Assert.AreEqual(7, recorder.LastNavigationArgument);
    }
}
