using System.Reflection;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using AtariGo.Client.Models;
using AtariGo.Client.Services;
using AtariGo.Client.Tests.Services;
using AtariGo.Client.ViewModels;
using AtariGo.Contracts;

namespace AtariGo.Client.Tests.ViewModels;

[TestClass]
[TestCategory("Unit")]
public sealed class PrivateRoomLobbyViewModelTests
{
    [TestMethod]
    public async Task WatchRoomAsync_OpponentJoins_DisplaysServerName()
    {
        // Arrange
        INavigationService navigation =
            DispatchProxy.Create<INavigationService, RecordingNavigationProxy>();
        var client = new StubRoomsClient();
        RoomSnapshot updatedRoom = client.Room.Clone();
        updatedRoom.Version++;
        updatedRoom.Participants.Add(new RoomParticipant { UserName = "Opponent" });
        client.Updates.Add(updatedRoom);
        using var viewModel = new PrivateRoomLobbyViewModel(
            navigation, client, new PrivateRoomNavigationContext(client.Room, IsHost: true));

        // Act
        await viewModel.WatchRoomAsync();

        // Assert
        Assert.AreEqual(client.Room.RoomId, client.Observation?.RoomId);
        Assert.AreEqual(client.Room.Version, client.Observation?.FromVersion);
        Assert.AreEqual("Opponent", viewModel.OpponentName);
    }

    [TestMethod]
    public async Task LeaveRoomCommand_ConfirmedByServer_ReturnsToMenu()
    {
        // Arrange
        INavigationService navigation =
            DispatchProxy.Create<INavigationService, RecordingNavigationProxy>();
        var client = new StubRoomsClient();
        using var viewModel = new PrivateRoomLobbyViewModel(
            navigation, client, new PrivateRoomNavigationContext(client.Room, IsHost: false));

        // Act
        await CommandExecution.ExecuteAsync(viewModel.LeaveRoomCommand, null);

        // Assert
        Assert.AreEqual(client.Room.RoomId, client.LeftRoomId);
        var recorder = (RecordingNavigationProxy)navigation;
        CollectionAssert.AreEqual(
            new[] { nameof(INavigationService.NavigateToMainMenu) }, recorder.Calls.ToArray());
    }
}
