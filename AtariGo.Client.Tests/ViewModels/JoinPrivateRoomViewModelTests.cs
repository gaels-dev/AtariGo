using System.Reflection;


using AtariGo.Client.Services;
using AtariGo.Client.Tests.Services;
using AtariGo.Client.ViewModels;

namespace AtariGo.Client.Tests.ViewModels;

[TestClass]
[TestCategory("Unit")]
public sealed class JoinPrivateRoomViewModelTests
{
    [TestMethod]
    public async Task JoinCommand_CodeWithSurroundingSpaces_SendsTrimmedCode()
    {
        // Arrange
        INavigationService navigation =
            DispatchProxy.Create<INavigationService, RecordingNavigationProxy>();
        var client = new StubRoomsClient();
        using var viewModel = new JoinPrivateRoomViewModel(navigation, client)
        {
            RoomCode = " AABBCCDDEE "
        };

        // Act
        await CommandExecution.ExecuteAsync(viewModel.JoinCommand, null);

        // Assert
        Assert.AreEqual("AABBCCDDEE", client.JoinedCode);
        var recorder = (RecordingNavigationProxy)navigation;
        CollectionAssert.AreEqual(
            new[] { nameof(INavigationService.NavigateToPrivateRoomLobby) },
            recorder.Calls.ToArray());
    }
}
