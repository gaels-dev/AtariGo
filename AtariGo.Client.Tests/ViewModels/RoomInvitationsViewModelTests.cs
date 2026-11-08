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
    public async Task SendCommand_SelectionChanged_SendsReviewedFriendAndRoomOnlyOnConfirmation()
    {
        // Arrange
        var client = new StubRoomInvitationsClient();
        var context = new RoomInvitationsNavigationContext(42, "ROOMCODE");
        context.Friends.Add(new FriendItemViewModel(2, "First", true));
        context.Friends.Add(new FriendItemViewModel(3, "Second", true));
        using RoomInvitationsViewModel model = Create(client, context);
        model.OpenCommand.Execute(null);
        model.SelectedFriend = context.Friends[0];
        model.SelectedFriend = context.Friends[1];
        string reviewed = model.SelectedRecipientLabel;
        int sentBeforeConfirmation = client.SendCount;

        // Act
        await CommandExecution.ExecuteAsync(model.SendCommand, null);

        // Assert
        Assert.AreEqual(0, sentBeforeConfirmation);
        StringAssert.Contains(reviewed, "Second");
        StringAssert.Contains(reviewed, "ROOMCODE");
        Assert.AreEqual(3, client.Sent?.RecipientUserId);
        Assert.AreEqual(42, client.Sent?.RoomId);
        Assert.AreNotEqual(string.Empty, model.Message);
        Assert.IsTrue(model.IsOpen);
    }

    [TestMethod]
    public void CloseCommand_SelectionPending_DoesNotCreateInvitation()
    {
        // Arrange
        var client = new StubRoomInvitationsClient();
        var context = new RoomInvitationsNavigationContext(42, "ROOMCODE");
        context.Friends.Add(new FriendItemViewModel(2, "Friend", true));
        using RoomInvitationsViewModel model = Create(client, context);
        model.OpenCommand.Execute(null);
        model.SelectedFriend = context.Friends[0];

        // Act
        model.CloseCommand.Execute(null);

        // Assert
        Assert.IsFalse(model.IsOpen);
        Assert.IsNull(model.SelectedFriend);
        Assert.AreEqual(0, client.SendCount);
    }

    [TestMethod]
    public async Task SendCommand_FriendDisconnectsOrIsRemoved_DoesNotSend()
    {
        // Arrange
        var client = new StubRoomInvitationsClient();
        var context = new RoomInvitationsNavigationContext(42, "ROOMCODE");
        var friend = new FriendItemViewModel(2, "Friend", true);
        context.Friends.Add(friend);
        using RoomInvitationsViewModel model = Create(client, context);
        model.SelectedFriend = friend;
        friend.IsOnline = false;

        // Act
        await CommandExecution.ExecuteAsync(model.SendCommand, null);
        friend.IsOnline = true;
        context.Friends.Remove(friend);
        await CommandExecution.ExecuteAsync(model.SendCommand, null);

        // Assert
        Assert.IsFalse(model.CanSend);
        Assert.IsNull(model.SelectedFriend);
        Assert.AreEqual(0, client.SendCount);
    }

    [TestMethod]
    public async Task AcceptCommand_ConfirmedResponse_OpensPrivateLobby()
    {
        // Arrange
        var client = IncomingClient();
        client.Response = new RespondRoomInvitationResponse
        {
            Room = new RoomSnapshot { RoomId = 42, State = RoomState.Waiting }
        };
        INavigationService navigation =
            DispatchProxy.Create<INavigationService, RecordingNavigationProxy>();
        using var model = new RoomInvitationsViewModel(navigation, client,
            new RoomInvitationsNavigationContext(0, string.Empty)) { IsAcceptanceAllowed = true };
        await CommandExecution.ExecuteAsync(model.RefreshCommand, null);
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
        var client = IncomingClient();
        using RoomInvitationsViewModel model = Create(client,
            new RoomInvitationsNavigationContext(0, string.Empty));
        await CommandExecution.ExecuteAsync(model.RefreshCommand, null);
        model.SelectedInvitation = model.Invitations[0];

        // Act
        await CommandExecution.ExecuteAsync(model.AcceptCommand, null);
        model.IsAcceptanceAllowed = true;
        client.ListResponse.Invitations[0].State = RoomInvitationState.Expired;
        await CommandExecution.ExecuteAsync(model.RefreshCommand, null);
        model.SelectedInvitation = model.Invitations[0];
        await CommandExecution.ExecuteAsync(model.AcceptCommand, null);

        // Assert
        Assert.IsNull(client.Decision);
        Assert.IsFalse(model.CanRespond);
    }

    [TestMethod]
    public async Task RespondCommands_RejectOrCancel_SendsSelectedInvitationId()
    {
        // Arrange
        var client = IncomingClient();
        using RoomInvitationsViewModel incoming = Create(client,
            new RoomInvitationsNavigationContext(0, string.Empty));
        incoming.IsAcceptanceAllowed = true;
        using RoomInvitationsViewModel outgoing = Create(client,
            new RoomInvitationsNavigationContext(42, "ROOMCODE"));
        await CommandExecution.ExecuteAsync(incoming.RefreshCommand, null);
        await CommandExecution.ExecuteAsync(outgoing.RefreshCommand, null);
        incoming.SelectedInvitation = incoming.Invitations[0];
        outgoing.SelectedInvitation = outgoing.Invitations[0];

        // Act
        await CommandExecution.ExecuteAsync(incoming.RejectCommand, null);
        await CommandExecution.ExecuteAsync(outgoing.CancelCommand, null);

        // Assert
        Assert.IsFalse(client.Decision?.Accept);
        Assert.AreEqual(7, client.Decision?.InvitationId);
        Assert.AreEqual(7, client.CancelledId);
    }

    [TestMethod]
    public async Task SendCommand_TransportFailure_RefreshesWithoutRetryingMutation()
    {
        // Arrange
        var client = new StubRoomInvitationsClient
        {
            SendHandler = () => Task.FromException<SendRoomInvitationResponse>(
                new RpcException(new Status(StatusCode.DeadlineExceeded, "Timeout")))
        };
        var context = new RoomInvitationsNavigationContext(42, "ROOMCODE");
        context.Friends.Add(new FriendItemViewModel(2, "Friend", true));
        using RoomInvitationsViewModel model = Create(client, context);
        model.SelectedFriend = context.Friends[0];

        // Act
        await CommandExecution.ExecuteAsync(model.SendCommand, null);

        // Assert
        Assert.AreEqual(1, client.SendCount);
        Assert.IsGreaterThan(0, client.ListCount);
        Assert.AreNotEqual(string.Empty, model.Message);
    }

    [TestMethod]
    public async Task Dispose_PendingAcceptance_DoesNotNavigateAfterLateResponse()
    {
        // Arrange
        var client = IncomingClient();
        var completion = new TaskCompletionSource<RespondRoomInvitationResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        client.RespondHandler = () => completion.Task;
        INavigationService navigation =
            DispatchProxy.Create<INavigationService, RecordingNavigationProxy>();
        using var model = new RoomInvitationsViewModel(navigation, client,
            new RoomInvitationsNavigationContext(0, string.Empty)) { IsAcceptanceAllowed = true };
        await CommandExecution.ExecuteAsync(model.RefreshCommand, null);
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
    public async Task RefreshCommand_ConcurrentNotification_IgnoresOlderSnapshot()
    {
        // Arrange
        var client = IncomingClient();
        var older = new TaskCompletionSource<ListRoomInvitationsResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        client.ListHandler = () => older.Task;
        using RoomInvitationsViewModel model = Create(client,
            new RoomInvitationsNavigationContext(0, string.Empty));
        Task refresh = CommandExecution.ExecuteAsync(model.RefreshCommand, null);
        client.ListHandler = null;
        client.ListResponse.Invitations[0].State = RoomInvitationState.Cancelled;

        // Act
        model.RequestRefresh();
        older.SetResult(new ListRoomInvitationsResponse());
        await refresh;

        // Assert
        Assert.HasCount(1, model.Invitations);
        Assert.AreEqual(RoomInvitationState.Cancelled, model.Invitations[0].Invitation.State);
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
