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
    public async Task JoinCommand_CodeWithSurroundingSpaces_SendsTrimmedCodeAsync()
    {
        // Arrange
        INavigationService navigation =
            DispatchProxy.Create<INavigationService, RecordingNavigationProxy>();
        var client = new StubRoomsClient();
        using var viewModel = new JoinPrivateRoomViewModel(navigation, client)
        {
            RoomCode = " az9q2m "
        };

        // Act
        await CommandExecution.ExecuteAsync(viewModel.JoinCommand, null);

        // Assert
        Assert.AreEqual("AZ9Q2M", client.JoinedCode);
        RecordingNavigationProxy recorder = (RecordingNavigationProxy)navigation;
        CollectionAssert.AreEqual(
            new[] { nameof(INavigationService.NavigateToPrivateRoomLobby) },
            recorder.Calls.ToArray());
    }

    [TestMethod]
    [DataRow("ABCDE")]
    [DataRow("ABCDEFG")]
    [DataRow("AABBCCDDEE")]
    [DataRow("AB-CD1")]
    [DataRow("ＡＢＣ１２３")]
    public async Task JoinCommand_InvalidCode_DoesNotSendRequestAsync(string code)
    {
        INavigationService navigation =
            DispatchProxy.Create<INavigationService, RecordingNavigationProxy>();
        StubRoomsClient client = new StubRoomsClient();
        using JoinPrivateRoomViewModel model = new JoinPrivateRoomViewModel(navigation, client)
        {
            RoomCode = code
        };

        await CommandExecution.ExecuteAsync(model.JoinCommand, null);

        Assert.IsNull(client.JoinedCode);
        Assert.IsTrue(model.ErrorMessage.Contains("6", StringComparison.Ordinal));
        Assert.AreEqual(0, ((RecordingNavigationProxy)navigation).Calls.Count);
    }
}
