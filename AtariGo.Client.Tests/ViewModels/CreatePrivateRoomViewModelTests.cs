using System.Reflection;

using AtariGo.Client.Services;
using AtariGo.Client.Models;
using AtariGo.Client.Tests.Services;
using AtariGo.Client.ViewModels;

namespace AtariGo.Client.Tests.ViewModels;

[TestClass]
[TestCategory("Unit")]
public sealed class CreatePrivateRoomViewModelTests
{
    [TestMethod]
    public async Task CreateRoomCommand_ConfirmedByServer_OpensPrivateLobbyAsync()
    {
        // Arrange
        INavigationService navigation =
            DispatchProxy.Create<INavigationService, RecordingNavigationProxy>();
        var client = new StubRoomsClient();
        using var viewModel = new CreatePrivateRoomViewModel(navigation, client)
        {
            SelectedCaptureGoal = 2
        };

        // Act
        await CommandExecution.ExecuteAsync(viewModel.CreateRoomCommand, null);

        // Assert
        Assert.AreEqual(2, client.CreatedConfiguration?.CaptureGoal);
        RecordingNavigationProxy recorder = (RecordingNavigationProxy)navigation;
        CollectionAssert.AreEqual(
            new[] { nameof(INavigationService.NavigateToPrivateRoomLobby) },
            recorder.Calls.ToArray());
    }

    [TestMethod]
    public async Task CreateRoomCommand_EmailRecipient_PreservesSelectionInPrivateLobbyAsync()
    {
        // Arrange
        INavigationService navigation =
            DispatchProxy.Create<INavigationService, RecordingNavigationProxy>();
        using var model = new CreatePrivateRoomViewModel(navigation, new StubRoomsClient())
        {
            EmailRecipientUserId = 7
        };

        // Act
        await CommandExecution.ExecuteAsync(model.CreateRoomCommand, null);

        // Assert
        RecordingNavigationProxy recorder = (RecordingNavigationProxy)navigation;
        PrivateRoomNavigationContext? context =
            recorder.LastNavigationArgument as PrivateRoomNavigationContext;
        Assert.IsNotNull(context);
        Assert.AreEqual(7, context.EmailRecipientUserId);
        Assert.IsTrue(context.IsHost);
    }
}
