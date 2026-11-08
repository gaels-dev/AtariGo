using System.Reflection;

using Grpc.Core;

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
        await viewModel.WatchRoomAsync(new PrivateRoomNavigationContext(client.Room, IsHost: true));

        // Assert
        Assert.AreEqual(client.Room.RoomId, client.Observation?.RoomId);
        Assert.AreEqual(client.Room.Version, client.Observation?.FromVersion);
        Assert.AreEqual("Opponent", viewModel.OpponentName);
    }

    [TestMethod]
    public async Task WatchRoomAsync_GameStarts_OpensSharedBoard()
    {
        // Arrange
        INavigationService navigation =
            DispatchProxy.Create<INavigationService, RecordingNavigationProxy>();
        var client = new StubRoomsClient();
        RoomSnapshot started = client.Room.Clone();
        started.GameId = 42;
        started.State = RoomState.Started;
        client.Updates.Add(started);
        var context = new PrivateRoomNavigationContext(client.Room, IsHost: false);
        using var viewModel = new PrivateRoomLobbyViewModel(navigation, client, context);

        // Act
        await viewModel.WatchRoomAsync(context);

        // Assert
        var recorder = (RecordingNavigationProxy)navigation;
        CollectionAssert.AreEqual(
            new[] { nameof(INavigationService.NavigateToSharedGame) }, recorder.Calls.ToArray());
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
    [TestMethod]
    public async Task SaveConfigurationCommand_Host_SendsVersionAndPreservesTimes()
    {
        // Arrange
        var client = CreateWaitingClient();
        using PrivateRoomLobbyViewModel viewModel = CreateLobby(client);
        viewModel.SelectedCaptureGoal = 3;
        int confirmedBeforeSave = viewModel.TargetCaptures;
        bool canStartWithDraft = viewModel.CanStartGame;

        // Act
        await CommandExecution.ExecuteAsync(viewModel.SaveConfigurationCommand, null);
        await CommandExecution.ExecuteAsync(viewModel.StartGameCommand, null);

        // Assert
        Assert.AreEqual(1, confirmedBeforeSave);
        Assert.IsFalse(canStartWithDraft);
        Assert.AreEqual(1, client.UpdateRequest?.ExpectedVersion);
        Assert.AreEqual(3, client.UpdateRequest?.Configuration.CaptureGoal);
        Assert.AreEqual(600, client.UpdateRequest?.Configuration.GameTimeSeconds);
        Assert.AreEqual(30, client.UpdateRequest?.Configuration.TurnTimeSeconds);
        Assert.AreEqual(3, viewModel.TargetCaptures);
        Assert.IsFalse(viewModel.HasPendingChanges);
        Assert.AreEqual(2, client.StartRequest?.ExpectedRoomVersion);
        Assert.AreEqual(string.Empty, viewModel.ErrorMessage);
    }

    [TestMethod]
    public async Task SaveConfigurationCommand_Guest_DoesNotCallServer()
    {
        // Arrange
        var client = CreateWaitingClient();
        using PrivateRoomLobbyViewModel viewModel = CreateLobby(client, false);
        viewModel.SelectedCaptureGoal = 3;

        // Act
        await CommandExecution.ExecuteAsync(viewModel.SaveConfigurationCommand, null);

        // Assert
        Assert.IsFalse(viewModel.CanEditConfiguration);
        Assert.IsNull(client.UpdateRequest);
        Assert.AreEqual(1, viewModel.TargetCaptures);
    }

    [TestMethod]
    public void DiscardConfigurationCommand_Draft_RestoresConfirmedSelection()
    {
        // Arrange
        var client = CreateWaitingClient();
        using PrivateRoomLobbyViewModel viewModel = CreateLobby(client);
        viewModel.SelectedCaptureGoal = 5;

        // Act
        viewModel.DiscardConfigurationCommand.Execute(null);

        // Assert
        Assert.AreEqual(1, viewModel.SelectedCaptureGoal);
        Assert.IsFalse(viewModel.HasPendingChanges);
        Assert.IsTrue(viewModel.CanStartGame);
        Assert.IsNull(client.UpdateRequest);
    }

    [TestMethod]
    public async Task WatchRoomAsync_NewerSnapshot_InvalidatesDraftAndIgnoresOlderSnapshot()
    {
        // Arrange
        var client = CreateWaitingClient();
        RoomSnapshot newer = client.Room.Clone();
        newer.Version = 3;
        newer.Configuration.CaptureGoal = 5;
        client.Updates.Add(newer);
        client.Updates.Add(client.Room.Clone());
        using PrivateRoomLobbyViewModel viewModel = CreateLobby(client);
        viewModel.SelectedCaptureGoal = 3;

        // Act
        await viewModel.WatchRoomAsync(new PrivateRoomNavigationContext(client.Room, true));

        // Assert
        Assert.AreEqual(5, viewModel.TargetCaptures);
        Assert.AreEqual(5, viewModel.SelectedCaptureGoal);
        Assert.IsFalse(viewModel.HasPendingChanges);
        Assert.AreNotEqual(string.Empty, viewModel.ErrorMessage);
    }

    [TestMethod]
    public async Task SaveConfigurationCommand_StaleVersion_RefreshesWithoutRetryingMutation()
    {
        // Arrange
        var client = CreateWaitingClient();
        client.UpdateHandler = _ => Task.FromException<RoomSnapshot>(
            new RpcException(new Status(StatusCode.Aborted, "Changed")));
        RoomSnapshot current = client.Room.Clone();
        current.Version = 2;
        current.Configuration.CaptureGoal = 5;
        client.Updates.Add(current);
        using PrivateRoomLobbyViewModel viewModel = CreateLobby(client);
        viewModel.SelectedCaptureGoal = 3;

        // Act
        await CommandExecution.ExecuteAsync(viewModel.SaveConfigurationCommand, null);

        // Assert
        Assert.IsTrue(client.Observations.Any(request => !request.HasFromVersion));
        Assert.AreEqual(5, viewModel.TargetCaptures);
        Assert.AreEqual(5, viewModel.SelectedCaptureGoal);
        Assert.IsTrue(viewModel.CanStartGame);
        Assert.AreNotEqual(string.Empty, viewModel.ErrorMessage);
    }

    [TestMethod]
    public async Task SaveConfigurationCommand_TransportFailure_BlocksUntilFreshSnapshot()
    {
        // Arrange
        var client = CreateWaitingClient();
        client.UpdateHandler = _ => Task.FromException<RoomSnapshot>(
            new RpcException(new Status(StatusCode.DeadlineExceeded, "Unconfirmed")));
        using PrivateRoomLobbyViewModel viewModel = CreateLobby(client);
        viewModel.SelectedCaptureGoal = 3;

        // Act
        await CommandExecution.ExecuteAsync(viewModel.SaveConfigurationCommand, null);
        int confirmedAfterFailure = viewModel.TargetCaptures;
        bool canEditAfterFailure = viewModel.CanEditConfiguration;
        bool canStartAfterFailure = viewModel.CanStartGame;
        RoomSnapshot serverRoom = client.Room.Clone();
        serverRoom.Version = 2;
        serverRoom.Configuration.CaptureGoal = 3;
        client.Updates.Add(serverRoom);
        await CommandExecution.ExecuteAsync(viewModel.RefreshConfigurationCommand, null);

        // Assert
        Assert.AreEqual(1, confirmedAfterFailure);
        Assert.IsFalse(canEditAfterFailure);
        Assert.IsFalse(canStartAfterFailure);
        Assert.AreEqual(3, viewModel.TargetCaptures);
        Assert.IsTrue(viewModel.CanStartGame);
    }

    [TestMethod]
    public async Task SaveConfigurationCommand_LateResponse_PreservesNewerObservedState()
    {
        // Arrange
        var client = CreateWaitingClient();
        var completion = new TaskCompletionSource<RoomSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        client.UpdateHandler = _ => completion.Task;
        using PrivateRoomLobbyViewModel viewModel = CreateLobby(client);
        viewModel.SelectedCaptureGoal = 3;
        Task save = CommandExecution.ExecuteAsync(viewModel.SaveConfigurationCommand, null);
        bool canEditDuringSave = viewModel.CanEditConfiguration;
        RoomSnapshot newer = client.Room.Clone();
        newer.Version = 3;
        newer.Configuration.CaptureGoal = 5;
        client.Updates.Add(newer);

        // Act
        await viewModel.WatchRoomAsync(new PrivateRoomNavigationContext(client.Room, true));
        RoomSnapshot older = newer.Clone();
        older.Version = 2;
        older.Configuration.CaptureGoal = 3;
        completion.SetResult(older);
        await save;

        // Assert
        Assert.IsFalse(canEditDuringSave);
        Assert.AreEqual(5, viewModel.TargetCaptures);
        Assert.AreEqual(5, viewModel.SelectedCaptureGoal);
    }

    [TestMethod]
    public async Task Dispose_PendingSave_DoesNotApplyLateResponse()
    {
        // Arrange
        var client = CreateWaitingClient();
        var completion = new TaskCompletionSource<RoomSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        client.UpdateHandler = _ => completion.Task;
        using PrivateRoomLobbyViewModel viewModel = CreateLobby(client);
        viewModel.SelectedCaptureGoal = 3;
        Task save = CommandExecution.ExecuteAsync(viewModel.SaveConfigurationCommand, null);

        // Act
        viewModel.Dispose();
        RoomSnapshot confirmed = client.Room.Clone();
        confirmed.Version = 2;
        confirmed.Configuration.CaptureGoal = 3;
        completion.SetResult(confirmed);
        await save;

        // Assert
        Assert.AreEqual(1, viewModel.TargetCaptures);
        Assert.AreEqual(string.Empty, viewModel.ErrorMessage);
    }

    private static StubRoomsClient CreateWaitingClient()
    {
        var client = new StubRoomsClient();
        client.Room.State = RoomState.Waiting;
        client.Room.Configuration = new RoomConfiguration
        {
            CaptureGoal = 1, GameTimeSeconds = 600, TurnTimeSeconds = 30
        };
        client.Room.Participants.Add(new RoomParticipant { UserName = "Host", IsHost = true });
        client.Room.Participants.Add(new RoomParticipant { UserName = "Guest", IsHost = false });
        return client;
    }

    private static PrivateRoomLobbyViewModel CreateLobby(StubRoomsClient client, bool isHost = true)
    {
        INavigationService navigation =
            DispatchProxy.Create<INavigationService, RecordingNavigationProxy>();
        return new PrivateRoomLobbyViewModel(
            navigation, client, new PrivateRoomNavigationContext(client.Room, isHost));
    }
}
