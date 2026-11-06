using System.Reflection;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using AtariGo.Client.Services;
using AtariGo.Client.Tests.Services;
using AtariGo.Client.ViewModels;

namespace AtariGo.Client.Tests.ViewModels;

[TestClass]
[TestCategory("Unit")]
public sealed class CreatePrivateRoomViewModelTests
{
    [TestMethod]
    public async Task CreateRoomCommand_ConfirmedByServer_OpensPrivateLobby()
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
        var recorder = (RecordingNavigationProxy)navigation;
        CollectionAssert.AreEqual(
            new[] { nameof(INavigationService.NavigateToPrivateRoomLobby) },
            recorder.Calls.ToArray());
    }
}
