using System.Reflection;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Grpc.Core;

using AtariGo.Client.Models;
using AtariGo.Client.Services;
using AtariGo.Client.Tests.Services;
using AtariGo.Client.ViewModels;
using AtariGo.Contracts;

namespace AtariGo.Client.Tests.ViewModels;

[TestClass]
[TestCategory("Unit")]
public sealed class RoomInvitationsViewModelTests
{
    [TestMethod]
    public async Task SendCommand_FriendRow_SendsOnlyTheClickedConnectedFriend()
    {
        // Arrange
        var client = new StubRoomInvitationsClient();
        var context = new RoomInvitationsNavigationContext(42);
        context.Friends.Add(new FriendItemViewModel(2, "First", true));
        context.Friends.Add(new FriendItemViewModel(3, "Second", true));
        context.Friends.Add(new FriendItemViewModel(4, "Offline", false));
        using RoomInvitationsViewModel model = Create(client, context);
        model.OpenCommand.Execute(null);
        int sentBeforeClick = client.SendCount;

        // Act
        await CommandExecution.ExecuteAsync(model.SendCommand, context.Friends[1]);

        // Assert
        Assert.AreEqual(0, sentBeforeClick);
        Assert.HasCount(2, model.OnlineFriends);
        Assert.AreEqual(3, client.Sent?.RecipientUserId);
        Assert.AreEqual(42, client.Sent?.RoomId);
        Assert.AreNotEqual(string.Empty, model.Message);
        Assert.IsTrue(model.IsOpen);
        Assert.AreEqual(0, client.ListCount);
    }

    [TestMethod]
    public void CloseCommand_BeforeInviting_DoesNotCreateInvitation()
    {
        // Arrange
        var client = new StubRoomInvitationsClient();
        using RoomInvitationsViewModel model = Create(
            client, new RoomInvitationsNavigationContext(42));
        model.OpenCommand.Execute(null);

        // Act
        model.CloseCommand.Execute(null);

        // Assert
        Assert.IsFalse(model.IsOpen);
        Assert.AreEqual(0, client.SendCount);
    }

    [TestMethod]
    public async Task OnlineFriends_PresenceAndMembershipChanges_UpdatesWithoutManualRefresh()
    {
        // Arrange
        var client = new StubRoomInvitationsClient();
        var context = new RoomInvitationsNavigationContext(42);
        var friend = new FriendItemViewModel(2, "Friend", false);
        context.Friends.Add(friend);
        using RoomInvitationsViewModel model = Create(client, context);
        int initiallyVisible = model.OnlineFriends.Count;

        // Act
        friend.IsOnline = true;
        int visibleAfterConnecting = model.OnlineFriends.Count;
        friend.IsOnline = false;
        await CommandExecution.ExecuteAsync(model.SendCommand, friend);
        friend.IsOnline = true;
        context.Friends.Remove(friend);
        await CommandExecution.ExecuteAsync(model.SendCommand, friend);

        // Assert
        Assert.AreEqual(0, initiallyVisible);
        Assert.AreEqual(1, visibleAfterConnecting);
        Assert.HasCount(0, model.OnlineFriends);
        Assert.AreEqual(0, client.SendCount);
    }

    [TestMethod]
    public async Task SendCommand_FullRoom_DoesNotInvite()
    {
        // Arrange
        var client = new StubRoomInvitationsClient();
        var context = new RoomInvitationsNavigationContext(42);
        context.Friends.Add(new FriendItemViewModel(2, "Friend", true));
        using RoomInvitationsViewModel model = Create(client, context);
        model.IsRoomAvailable = false;

        // Act
        await CommandExecution.ExecuteAsync(model.SendCommand, context.Friends[0]);

        // Assert
        Assert.IsNull(client.Sent);
    }

    [TestMethod]
    public async Task AcceptCommand_ConfirmedResponse_OpensPrivateLobby()
    {
        // Arrange
        StubRoomInvitationsClient client = IncomingClient();
        client.Response = new RespondRoomInvitationResponse
        {
            Room = new RoomSnapshot { RoomId = 42, State = RoomState.Waiting }
        };
        INavigationService navigation =
            DispatchProxy.Create<INavigationService, RecordingNavigationProxy>();
        using var model = new RoomInvitationsViewModel(navigation, client,
            new RoomInvitationsNavigationContext(0)) { IsAcceptanceAllowed = true };
        model.RequestRefresh();
        model.SelectedInvitation = model.Invitations[0];

        // Act
        await CommandExecution.ExecuteAsync(model.AcceptCommand, null);

        // Assert
        Assert.IsTrue(client.Decision?.Accept);
        Assert.AreEqual(7, client.Decision?.InvitationId);
        CollectionAssert.AreEqual(new[] { nameof(INavigationService.NavigateToPrivateRoomLobby) },
            ((RecordingNavigationProxy)navigation).Calls.ToArray());
    }

    [TestMethod]
    public async Task AcceptCommand_PlayerBusyOrInvitationExpired_DoesNotAccept()
    {
        // Arrange
        StubRoomInvitationsClient client = IncomingClient();
        using RoomInvitationsViewModel model = Create(
            client, new RoomInvitationsNavigationContext(0));
        model.RequestRefresh();
        model.SelectedInvitation = model.Invitations[0];

        // Act
        await CommandExecution.ExecuteAsync(model.AcceptCommand, null);
        model.IsAcceptanceAllowed = true;
        client.ListResponse.Invitations[0].State = RoomInvitationState.Expired;
        model.RequestRefresh();
        await CommandExecution.ExecuteAsync(model.AcceptCommand, null);

        // Assert
        Assert.IsNull(client.Decision);
        Assert.IsFalse(model.CanRespond);
    }

    [TestMethod]
    public async Task RejectCommand_SelectedInvitation_SendsDecision()
    {
        // Arrange
        StubRoomInvitationsClient client = IncomingClient();
        using RoomInvitationsViewModel model = Create(
            client, new RoomInvitationsNavigationContext(0));
        model.IsAcceptanceAllowed = true;
        model.RequestRefresh();
        model.SelectedInvitation = model.Invitations[0];

        // Act
        await CommandExecution.ExecuteAsync(model.RejectCommand, null);

        // Assert
        Assert.IsFalse(client.Decision?.Accept);
        Assert.AreEqual(7, client.Decision?.InvitationId);
    }

    [TestMethod]
    public async Task SendCommand_TransportFailure_DoesNotReplayMutation()
    {
        // Arrange
        var client = new StubRoomInvitationsClient
        {
            SendHandler = () => Task.FromException<SendRoomInvitationResponse>(
                new RpcException(new Status(StatusCode.DeadlineExceeded, "Timeout")))
        };
        var context = new RoomInvitationsNavigationContext(42);
        context.Friends.Add(new FriendItemViewModel(2, "Friend", true));
        using RoomInvitationsViewModel model = Create(client, context);

        // Act
        await CommandExecution.ExecuteAsync(model.SendCommand, context.Friends[0]);

        // Assert
        Assert.AreEqual(1, client.SendCount);
        Assert.AreNotEqual(string.Empty, model.Message);
    }

    [TestMethod]
    public async Task Dispose_PendingAcceptance_DoesNotNavigateAfterLateResponse()
    {
        // Arrange
        StubRoomInvitationsClient client = IncomingClient();
        var completion = new TaskCompletionSource<RespondRoomInvitationResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        client.RespondHandler = () => completion.Task;
        INavigationService navigation =
            DispatchProxy.Create<INavigationService, RecordingNavigationProxy>();
        using var model = new RoomInvitationsViewModel(navigation, client,
            new RoomInvitationsNavigationContext(0)) { IsAcceptanceAllowed = true };
        model.RequestRefresh();
        model.SelectedInvitation = model.Invitations[0];
        Task accepting = CommandExecution.ExecuteAsync(model.AcceptCommand, null);

        // Act
        model.Dispose();
        completion.SetResult(new RespondRoomInvitationResponse
        {
            Room = new RoomSnapshot { RoomId = 42 }
        });
        await accepting;

        // Assert
        Assert.HasCount(0, ((RecordingNavigationProxy)navigation).Calls);
    }

    [TestMethod]
    public async Task RequestRefresh_NotificationDuringLoad_ReconcilesLatestStateAutomatically()
    {
        // Arrange
        StubRoomInvitationsClient client = IncomingClient();
        var completion = new TaskCompletionSource<ListRoomInvitationsResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        client.ListHandler = () => completion.Task;
        using RoomInvitationsViewModel model = Create(
            client, new RoomInvitationsNavigationContext(0));
        model.RequestRefresh();
        client.ListHandler = null;
        client.ListResponse.Invitations[0].State = RoomInvitationState.Cancelled;

        // Act
        model.RequestRefresh();
        completion.SetResult(new ListRoomInvitationsResponse());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (model.Invitations.Count == 0)
        {
            await Task.Delay(10, timeout.Token);
        }

        // Assert
        Assert.AreEqual(RoomInvitationState.Cancelled, model.Invitations[0].Invitation.State);
        Assert.AreEqual(2, client.ListCount);
    }

    [TestMethod]
    public void Dispose_FriendCollection_RemovesPresenceSubscriptions()
    {
        // Arrange
        var context = new RoomInvitationsNavigationContext(42);
        var friend = new FriendItemViewModel(2, "Friend", true);
        context.Friends.Add(friend);
        using RoomInvitationsViewModel model = Create(new StubRoomInvitationsClient(), context);

        // Act
        model.Dispose();
        friend.IsOnline = false;
        context.Friends.Clear();

        // Assert
        Assert.HasCount(1, model.OnlineFriends);
    }

    private static StubRoomInvitationsClient IncomingClient()
    {
        var client = new StubRoomInvitationsClient();
        client.ListResponse.Invitations.Add(new RoomInvitation
        {
            InvitationId = 7, RoomId = 42, RoomCode = "ROOMCODE", SenderName = "Host",
            RecipientName = "Friend", State = RoomInvitationState.Pending
        });
        return client;
    }

    private static RoomInvitationsViewModel Create(
        StubRoomInvitationsClient client, RoomInvitationsNavigationContext context)
    {
        INavigationService navigation =
            DispatchProxy.Create<INavigationService, RecordingNavigationProxy>();
        return new RoomInvitationsViewModel(navigation, client, context);
    }
}
