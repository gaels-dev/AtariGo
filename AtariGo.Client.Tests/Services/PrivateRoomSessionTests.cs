using System.Runtime.CompilerServices;
using System.Threading.Channels;

using Grpc.Core;

using AtariGo.Client.Models;
using AtariGo.Client.Services;
using AtariGo.Contracts;

namespace AtariGo.Client.Tests.Services;

[TestClass]
[TestCategory("Unit")]
public sealed class PrivateRoomSessionTests
{
    private static readonly TimeSpan completionTimeout = TimeSpan.FromSeconds(5);

    [TestMethod]
    public async Task WatchAsync_ConcurrentCalls_SharesOneObservation()
    {
        // Arrange
        StubRoomsClient client = CreateClient();
        Channel<RoomSnapshot> updates = Channel.CreateUnbounded<RoomSnapshot>();
        client.WatchHandler = (_, token) => updates.Reader.ReadAllAsync(token);
        using PrivateRoomSession session = new PrivateRoomSession(client, client.Room);
        // Act
        Task first = session.WatchAsync(CancellationToken.None);
        Task second = session.WatchAsync(CancellationToken.None);
        session.Dispose();
        await Task.WhenAll(first, second).WaitAsync(completionTimeout);
        // Assert
        Assert.AreSame(first, second);
        Assert.IsTrue(client.Observations.Count <= 1);
    }

    [TestMethod]
    public async Task WatchAsync_StaleOrForeignSnapshots_KeepsLatestConfirmedRoom()
    {
        // Arrange
        StubRoomsClient client = CreateClient();
        RoomSnapshot latest = client.Room.Clone();
        latest.Version = 3;
        latest.Configuration.CaptureGoal = 5;
        RoomSnapshot stale = client.Room.Clone();
        stale.Version = 2;
        RoomSnapshot foreign = latest.Clone();
        foreign.RoomId++;
        foreign.Version = 4;
        RoomSnapshot closed = latest.Clone();
        closed.State = RoomState.Closed;
        closed.Version = 4;
        client.Updates.AddRange(new[] { latest, stale, foreign, closed });
        using PrivateRoomSession session = new PrivateRoomSession(client, client.Room);
        List<long> versions = new List<long>();
        session.Updated += update => versions.Add(update.Room.Version);
        // Act
        await session.WatchAsync(CancellationToken.None).WaitAsync(completionTimeout);
        // Assert
        Assert.AreEqual(client.Room.RoomId, session.Room.RoomId);
        Assert.AreEqual(5, session.Room.Configuration.CaptureGoal);
        CollectionAssert.AreEqual(new long[] { 1, 3, 4 }, versions);
    }

    [TestMethod]
    [DataRow(StatusCode.Unauthenticated, ClientFailureReason.SessionExpired)]
    [DataRow(StatusCode.PermissionDenied, ClientFailureReason.PermissionDenied)]
    [DataRow(StatusCode.NotFound, ClientFailureReason.NotFound)]
    public async Task WatchAsync_PermanentFailure_ReportsApplicationReasonWithoutRetry(
        StatusCode statusCode, ClientFailureReason expected)
    {
        // Arrange
        StubRoomsClient client = CreateClient();
        client.WatchHandler = (_, token) => FailedStream(statusCode, token);
        using PrivateRoomSession session = new PrivateRoomSession(client, client.Room);
        PrivateRoomSessionUpdate? last = null;
        session.Updated += update => last = update;
        // Act
        await session.WatchAsync(CancellationToken.None).WaitAsync(completionTimeout);
        // Assert
        Assert.AreEqual(expected, last!.Failure);
        Assert.IsFalse(last.IsSynchronized);
        Assert.HasCount(1, client.Observations);
    }

    [TestMethod]
    public async Task WatchAsync_DisposedOwner_CancelsObservationAndIgnoresLateSnapshot()
    {
        // Arrange
        StubRoomsClient client = CreateClient();
        TaskCompletionSource<RoomSnapshot> response = new TaskCompletionSource<RoomSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource entered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        client.WatchHandler = (_, token) => LateSnapshotAsync(response.Task, entered, token);
        using PrivateRoomSession session = new PrivateRoomSession(client, client.Room);
        int notifications = 0;
        session.Updated += _ => notifications++;
        Task watching = session.WatchAsync(CancellationToken.None);
        await entered.Task.WaitAsync(completionTimeout);
        int before = notifications;
        RoomSnapshot late = client.Room.Clone();
        late.Version++;
        late.Configuration.CaptureGoal = 5;
        // Act
        session.Dispose();
        response.SetResult(late);
        await watching.WaitAsync(completionTimeout);
        // Assert
        Assert.AreEqual(before, notifications);
        Assert.AreEqual(1, session.Room.Configuration.CaptureGoal);
        Assert.AreEqual(1, session.Room.Version);
    }

    [TestMethod]
    public async Task StartGameAsync_StartReplyLost_RecoversStartedRoomWithoutReplaying()
    {
        // Arrange
        StubRoomsClient client = CreateClient();
        client.StartHandler = _ => Task.FromException(new RpcException(
            new Status(StatusCode.DeadlineExceeded, "Reply lost.")));
        RoomSnapshot started = client.Room.Clone();
        started.Version++;
        started.State = RoomState.Started;
        started.GameId = 42;
        client.Updates.Add(started);
        using PrivateRoomSession session = new PrivateRoomSession(client, client.Room);
        PrivateRoomSessionUpdate? last = null;
        session.Updated += update => last = update;
        // Act
        await session.StartGameAsync(1, CancellationToken.None);
        // Assert
        CollectionAssert.AreEqual(new[] { "Start" }, client.Operations);
        Assert.AreEqual(42, session.Room.GameId);
        Assert.AreEqual(RoomState.Started, session.Room.State);
        Assert.IsFalse(client.Observation!.HasFromVersion);
        Assert.IsTrue(last!.IsSynchronized);
        Assert.IsTrue(last.IsStartRecovery);
    }

    [TestMethod]
    public async Task StartGameAsync_ConfirmationFails_RejectsStaleRecoverySnapshot()
    {
        // Arrange
        StubRoomsClient client = CreateClient();
        client.Room.Version = 3;
        client.UpdateHandler = _ => Task.FromException<RoomSnapshot>(new RpcException(
            new Status(StatusCode.Aborted, "Changed.")));
        RoomSnapshot stale = client.Room.Clone();
        stale.Version = 2;
        RoomSnapshot current = client.Room.Clone();
        current.Version = 4;
        current.Configuration.CaptureGoal = 3;
        client.Updates.AddRange(new[] { stale, current });
        using PrivateRoomSession session = new PrivateRoomSession(client, client.Room);
        // Act
        await session.StartGameAsync(3, CancellationToken.None);
        // Assert
        Assert.AreEqual(4, session.Room.Version);
        Assert.AreEqual(3, session.Room.Configuration.CaptureGoal);
        CollectionAssert.AreEqual(new[] { "Update" }, client.Operations);
        Assert.IsNull(client.StartRequest);
    }

    [TestMethod]
    public void Room_ExternalSnapshotIsModified_DoesNotChangeConfirmedState()
    {
        // Arrange
        StubRoomsClient client = CreateClient();
        using PrivateRoomSession session = new PrivateRoomSession(client, client.Room);
        // Act
        RoomSnapshot snapshot = session.Room;
        snapshot.Configuration.CaptureGoal = 5;
        client.Room.Configuration.CaptureGoal = 3;
        // Assert
        Assert.AreEqual(1, session.Room.Configuration.CaptureGoal);
    }

    [TestMethod]
    [DataRow(StatusCode.NotFound, true, ClientFailureReason.None)]
    [DataRow(StatusCode.Unavailable, false, ClientFailureReason.Unavailable)]
    public async Task LeaveAsync_RemoteFailure_ReturnsApplicationResult(
        StatusCode statusCode, bool expectedLeft, ClientFailureReason expectedFailure)
    {
        // Arrange
        StubRoomsClient client = CreateClient();
        client.LeaveHandler = _ => Task.FromException(new RpcException(
            new Status(statusCode, "Leave failed.")));
        using PrivateRoomSession session = new PrivateRoomSession(client, client.Room);
        ClientFailureReason failure = ClientFailureReason.None;
        session.Updated += update => failure = update.Failure;
        // Act
        bool left = await session.LeaveAsync(CancellationToken.None);
        // Assert
        Assert.AreEqual(expectedLeft, left);
        Assert.AreEqual(expectedFailure, failure);
    }

    [TestMethod]
    public async Task WatchAsync_CallerCancels_StopsWithoutReportingFailure()
    {
        // Arrange
        StubRoomsClient client = CreateClient();
        Channel<RoomSnapshot> updates = Channel.CreateUnbounded<RoomSnapshot>();
        TaskCompletionSource entered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        client.WatchHandler = (_, token) =>
        {
            entered.SetResult();
            return updates.Reader.ReadAllAsync(token);
        };
        using PrivateRoomSession session = new PrivateRoomSession(client, client.Room);
        using CancellationTokenSource cancellation = new CancellationTokenSource();
        ClientFailureReason failure = ClientFailureReason.None;
        session.Updated += update => failure = update.Failure;
        Task watching = session.WatchAsync(cancellation.Token);
        await entered.Task.WaitAsync(completionTimeout);
        // Act
        cancellation.Cancel();
        await watching.WaitAsync(completionTimeout);
        // Assert
        Assert.AreEqual(ClientFailureReason.None, failure);
        Assert.HasCount(1, client.Observations);
    }

    private static StubRoomsClient CreateClient()
    {
        StubRoomsClient client = new StubRoomsClient();
        client.Room.State = RoomState.Waiting;
        client.Room.Configuration = new RoomConfiguration
        {
            CaptureGoal = 1,
            GameTimeSeconds = 600,
            TurnTimeSeconds = 30
        };
        client.Room.Participants.Add(new RoomParticipant { UserName = "Host", IsHost = true });
        client.Room.Participants.Add(new RoomParticipant { UserName = "Guest" });
        return client;
    }

    private static async IAsyncEnumerable<RoomSnapshot> FailedStream(StatusCode statusCode,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.FromException(new RpcException(new Status(statusCode, "Unavailable.")));
        yield break;
    }

    private static async IAsyncEnumerable<RoomSnapshot> LateSnapshotAsync(
        Task<RoomSnapshot> response,
        TaskCompletionSource entered, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        entered.SetResult();
        yield return await response;
        cancellationToken.ThrowIfCancellationRequested();
    }
}
