using System.Reflection;
using System.Runtime.CompilerServices;


using Grpc.Core;

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
    public async Task StartGameCommand_PendingConfiguration_ConfirmsBeforeStarting()
    {
        // Arrange
        StubRoomsClient client = CreateWaitingClient();
        using PrivateRoomLobbyViewModel model = CreateLobby(client);
        model.SelectedCaptureGoal = 3;
        int confirmedBeforeClick = model.TargetCaptures;
        bool canStartWithDraft = model.CanStartGame;

        // Act
        await CommandExecution.ExecuteAsync(model.StartGameCommand, null);

        // Assert
        Assert.IsTrue(canStartWithDraft);
        Assert.AreEqual(1, confirmedBeforeClick);
        CollectionAssert.AreEqual(new[] { "Update", "Start" }, client.Operations);
        Assert.AreEqual(3, client.UpdateRequest?.Configuration.CaptureGoal);
        Assert.AreEqual(600, client.UpdateRequest?.Configuration.GameTimeSeconds);
        Assert.AreEqual(30, client.UpdateRequest?.Configuration.TurnTimeSeconds);
        Assert.AreEqual(1, client.UpdateRequest?.ExpectedVersion);
        Assert.AreEqual(2, client.StartRequest?.ExpectedRoomVersion);
    }

    [TestMethod]
    public async Task StartGameCommand_UnchangedConfiguration_DoesNotSendRedundantUpdate()
    {
        // Arrange
        StubRoomsClient client = CreateWaitingClient();
        using PrivateRoomLobbyViewModel model = CreateLobby(client);

        // Act
        await CommandExecution.ExecuteAsync(model.StartGameCommand, null);

        // Assert
        CollectionAssert.AreEqual(new[] { "Start" }, client.Operations);
        Assert.IsNull(client.UpdateRequest);
        Assert.AreEqual(1, client.StartRequest?.ExpectedRoomVersion);
    }

    [TestMethod]
    public async Task StartGameCommand_Opponent_DoesNotModifyOrStartRoom()
    {
        // Arrange
        StubRoomsClient client = CreateWaitingClient();
        using PrivateRoomLobbyViewModel model = CreateLobby(client, false);
        model.SelectedCaptureGoal = 5;

        // Act
        await CommandExecution.ExecuteAsync(model.StartGameCommand, null);

        // Assert
        Assert.IsFalse(model.CanEditConfiguration);
        Assert.HasCount(0, client.Operations);
    }

    [TestMethod]
    public async Task WatchRoomAsync_ParticipantsChange_PreservesPendingConfiguration()
    {
        // Arrange
        StubRoomsClient client = CreateWaitingClient();
        RoomSnapshot joined = client.Room.Clone();
        joined.Version++;
        joined.Participants[1].UserName = "New opponent";
        client.WatchHandler = (_, token) => StreamRooms(new[] { joined }, token);
        using PrivateRoomLobbyViewModel model = CreateLobby(client);
        model.SelectedCaptureGoal = 5;

        // Act
        Task observing = model.WatchRoomAsync();
        await WaitUntilAsync(() => model.OpponentName == "New opponent");
        model.Dispose();
        await observing;

        // Assert
        Assert.AreEqual(5, model.SelectedCaptureGoal);
        Assert.AreEqual(1, model.TargetCaptures);
        Assert.IsNull(client.UpdateRequest);
    }

    [TestMethod]
    public async Task WatchRoomAsync_StartReceived_OpensSharedBoard()
    {
        // Arrange
        StubRoomsClient client = CreateWaitingClient();
        RoomSnapshot started = client.Room.Clone();
        started.State = RoomState.Started;
        started.GameId = 42;
        started.Version++;
        client.Updates.Add(started);
        INavigationService navigation =
            DispatchProxy.Create<INavigationService, RecordingNavigationProxy>();
        using var model = new PrivateRoomLobbyViewModel(navigation, client,
            new PrivateRoomNavigationContext(client.Room, false));

        // Act
        await model.WatchRoomAsync();

        // Assert
        CollectionAssert.AreEqual(new[] { nameof(INavigationService.NavigateToSharedGame) },
            ((RecordingNavigationProxy)navigation).Calls.ToArray());
    }

    [TestMethod]
    public async Task StartGameCommand_UpdateUnconfirmed_RecoversWithoutStartingOrReplaying()
    {
        // Arrange
        StubRoomsClient client = CreateWaitingClient();
        client.UpdateHandler = _ => Task.FromException<RoomSnapshot>(
            new RpcException(new Status(StatusCode.DeadlineExceeded, "Unconfirmed")));
        RoomSnapshot recovered = client.Room.Clone();
        recovered.Version++;
        recovered.Configuration.CaptureGoal = 3;
        client.Updates.Add(recovered);
        using PrivateRoomLobbyViewModel model = CreateLobby(client);
        model.SelectedCaptureGoal = 3;

        // Act
        await CommandExecution.ExecuteAsync(model.StartGameCommand, null);

        // Assert
        CollectionAssert.AreEqual(new[] { "Update" }, client.Operations);
        Assert.IsFalse(client.Observation!.HasFromVersion);
        Assert.AreEqual(3, model.TargetCaptures);
        Assert.IsNull(client.StartRequest);
        Assert.AreNotEqual(string.Empty, model.ErrorMessage);
    }

    [TestMethod]
    public async Task StartGameCommand_VersionConflict_RecoversAndRequiresAnotherClick()
    {
        // Arrange
        StubRoomsClient client = CreateWaitingClient();
        client.StartHandler = _ => Task.FromException(
            new RpcException(new Status(StatusCode.Aborted, "Changed")));
        RoomSnapshot recovered = client.Room.Clone();
        recovered.Version = 3;
        recovered.Configuration.CaptureGoal = 3;
        client.Updates.Add(recovered);
        using PrivateRoomLobbyViewModel model = CreateLobby(client);
        model.SelectedCaptureGoal = 3;

        // Act
        await CommandExecution.ExecuteAsync(model.StartGameCommand, null);

        // Assert
        CollectionAssert.AreEqual(new[] { "Update", "Start" }, client.Operations);
        Assert.AreEqual(3, model.TargetCaptures);
        Assert.AreNotEqual(string.Empty, model.ErrorMessage);
    }

    [TestMethod]
    public async Task WatchRoomAsync_TransportFailure_ReconnectsAutomaticallyWithFullSnapshot()
    {
        // Arrange
        StubRoomsClient client = CreateWaitingClient();
        RoomSnapshot recovered = client.Room.Clone();
        recovered.Version++;
        recovered.Configuration.CaptureGoal = 5;
        client.WatchHandler = (_, token) => client.Observations.Count == 1
            ? FailedStream() : StreamRooms(new[] { recovered }, token);
        using PrivateRoomLobbyViewModel model = CreateLobby(client);

        // Act
        Task observing = model.WatchRoomAsync();
        await WaitUntilAsync(() => model.TargetCaptures == 5);
        bool recoveredAutomatically = model.CanEditConfiguration;
        model.Dispose();
        await observing;

        // Assert
        Assert.IsTrue(recoveredAutomatically);
        Assert.HasCount(2, client.Observations);
        Assert.IsFalse(client.Observations[1].HasFromVersion);
        Assert.AreEqual(string.Empty, model.ErrorMessage);
    }

    [TestMethod]
    public async Task Dispose_PendingConfigurationConfirmation_DoesNotStartOrApplyLateResponse()
    {
        // Arrange
        StubRoomsClient client = CreateWaitingClient();
        var completion = new TaskCompletionSource<RoomSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        client.UpdateHandler = _ => completion.Task;
        using PrivateRoomLobbyViewModel model = CreateLobby(client);
        model.SelectedCaptureGoal = 3;
        Task starting = CommandExecution.ExecuteAsync(model.StartGameCommand, null);
        RoomSnapshot confirmed = client.Room.Clone();
        confirmed.Version++;
        confirmed.Configuration.CaptureGoal = 3;

        // Act
        model.Dispose();
        completion.SetResult(confirmed);
        await starting;

        // Assert
        Assert.AreEqual(1, model.TargetCaptures);
        Assert.IsNull(client.StartRequest);
    }

    [TestMethod]
    public async Task LeaveRoomCommand_ConfirmedByServer_ReturnsToMenu()
    {
        // Arrange
        StubRoomsClient client = CreateWaitingClient();
        INavigationService navigation =
            DispatchProxy.Create<INavigationService, RecordingNavigationProxy>();
        using var model = new PrivateRoomLobbyViewModel(navigation, client,
            new PrivateRoomNavigationContext(client.Room, true));

        // Act
        await CommandExecution.ExecuteAsync(model.LeaveRoomCommand, null);

        // Assert
        Assert.AreEqual(client.Room.RoomId, client.LeftRoomId);
        CollectionAssert.AreEqual(new[] { nameof(INavigationService.NavigateToMainMenu) },
            ((RecordingNavigationProxy)navigation).Calls.ToArray());
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
        client.Room.Participants.Add(new RoomParticipant { UserName = "Guest" });
        return client;
    }

    private static PrivateRoomLobbyViewModel CreateLobby(StubRoomsClient client, bool host = true)
    {
        INavigationService navigation =
            DispatchProxy.Create<INavigationService, RecordingNavigationProxy>();
        return new PrivateRoomLobbyViewModel(navigation, client,
            new PrivateRoomNavigationContext(client.Room, host));
    }

    private static async IAsyncEnumerable<RoomSnapshot> StreamRooms(
        IEnumerable<RoomSnapshot> rooms,
        [EnumeratorCancellation] CancellationToken token)
    {
        foreach (RoomSnapshot room in rooms)
        {
            yield return room;
        }

        await Task.Delay(Timeout.InfiniteTimeSpan, token);
    }

    private static async IAsyncEnumerable<RoomSnapshot> FailedStream()
    {
        await Task.FromException(new RpcException(
            new Status(StatusCode.Unavailable, "Disconnected")));
        yield break;
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }
}
