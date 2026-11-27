using System.Reflection;

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
    public async Task SendCommand_MenuRecipient_WaitsForConfirmationAndTargetsChosenFriendAsync()
    {
        // Arrange
        var client = new StubRoomInvitationsClient();
        var context = new RoomInvitationsNavigationContext(42) { InvitationRecipientUserId = 7 };
        var other = new FriendItemViewModel(8, "Other", true);
        var friend = new FriendItemViewModel(7, "Chosen", true);
        context.Friends.Add(other);
        context.Friends.Add(friend);
        using RoomInvitationsViewModel model = Create(client, context);
        Assert.IsTrue(model.IsOpen);
        CollectionAssert.AreEqual(new[] { friend }, model.OnlineFriends.ToArray());
        Assert.AreSame(friend, model.SelectedEmailFriend);
        Assert.AreEqual(0, client.SendCount);

        // Act
        await CommandExecution.ExecuteAsync(model.SendCommand, other);
        Assert.AreEqual(0, client.SendCount);
        await CommandExecution.ExecuteAsync(model.SendCommand, friend);

        // Assert
        Assert.AreEqual(1, client.SendCount);
        Assert.AreEqual(7, client.Sent?.RecipientUserId);
        Assert.AreEqual(42, client.Sent?.RoomId);
        Assert.AreEqual(RoomInvitationDeliveryChannel.InApp, client.Sent?.DeliveryChannel);
    }

    [TestMethod]
    public async Task CloseCommand_MenuRecipient_RestoresFullOnlineListWithoutSendingAsync()
    {
        // Arrange
        var client = new StubRoomInvitationsClient();
        var context = new RoomInvitationsNavigationContext(42) { InvitationRecipientUserId = 7 };
        context.Friends.Add(new FriendItemViewModel(7, "Chosen", true));
        context.Friends.Add(new FriendItemViewModel(8, "Other", true));
        using RoomInvitationsViewModel model = Create(client, context);

        // Act
        model.CloseCommand.Execute(null);

        // Assert
        Assert.IsFalse(model.IsOpen);
        Assert.AreEqual(0, client.SendCount);
        Assert.HasCount(2, model.OnlineFriends);
        model.OpenCommand.Execute(null);
        await CommandExecution.ExecuteAsync(model.SendCommand, context.Friends[1]);
        Assert.AreEqual(8, client.Sent?.RecipientUserId);
        Assert.AreEqual(1, client.SendCount);
    }

    [TestMethod]
    public async Task SendCommand_MenuRecipientDisconnects_AllowsOnlyExplicitEmailAsync()
    {
        // Arrange
        var client = new StubRoomInvitationsClient();
        var context = new RoomInvitationsNavigationContext(42) { InvitationRecipientUserId = 7 };
        var friend = new FriendItemViewModel(7, "Chosen", true);
        context.Friends.Add(friend);
        context.Friends.Add(new FriendItemViewModel(8, "Other", true));
        using RoomInvitationsViewModel model = Create(client, context);

        // Act
        friend.IsOnline = false;
        await CommandExecution.ExecuteAsync(model.SendCommand, friend);

        // Assert
        Assert.HasCount(0, model.OnlineFriends);
        Assert.AreEqual(0, client.SendCount);
        Assert.IsTrue(model.CanSendEmail);
        await CommandExecution.ExecuteAsync(model.SendEmailCommand, null);
        Assert.AreEqual(7, client.Sent?.RecipientUserId);
        Assert.AreEqual(RoomInvitationDeliveryChannel.Email, client.Sent?.DeliveryChannel);
        Assert.AreEqual(1, client.SendCount);
    }

    [TestMethod]
    public async Task SendCommand_MenuRecipientRemoved_DoesNotInviteAnotherFriendAsync()
    {
        // Arrange
        var client = new StubRoomInvitationsClient();
        var context = new RoomInvitationsNavigationContext(42) { InvitationRecipientUserId = 7 };
        context.Friends.Add(new FriendItemViewModel(8, "Other", true));
        using RoomInvitationsViewModel model = Create(client, context);

        // Act
        await CommandExecution.ExecuteAsync(model.SendCommand, context.Friends[0]);
        await CommandExecution.ExecuteAsync(model.SendEmailCommand, null);

        // Assert
        Assert.HasCount(0, model.OnlineFriends);
        Assert.IsNull(model.SelectedEmailFriend);
        Assert.IsFalse(model.CanSendEmail);
        Assert.AreEqual(0, client.SendCount);
    }

    [TestMethod]
    public async Task
        SendEmailCommand_SelectedOfflineFriend_WaitsForConfirmationAndSendsOnlyOnceAsync()
    {
        // Arrange
        var client = new StubRoomInvitationsClient
        {
            SendHandler = () => Task.FromResult(new SendRoomInvitationResponse
            {
                DeliveryStatus = RoomInvitationDeliveryStatus.EmailAccepted
            })
        };
        var context = new RoomInvitationsNavigationContext(42) { InvitationRecipientUserId = 7 };
        var friend = new FriendItemViewModel(7, "Offline", false);
        context.Friends.Add(friend);
        using RoomInvitationsViewModel model = Create(client, context);
        Assert.AreEqual(0, client.SendCount);

        // Act
        await CommandExecution.ExecuteAsync(model.SendEmailCommand, null);

        // Assert
        Assert.IsTrue(model.IsOpen);
        Assert.AreSame(friend, model.SelectedEmailFriend);
        Assert.AreEqual(1, client.SendCount);
        Assert.AreEqual(7, client.Sent?.RecipientUserId);
        Assert.AreEqual(42, client.Sent?.RoomId);
        Assert.AreEqual(RoomInvitationDeliveryChannel.Email, client.Sent?.DeliveryChannel);
        Assert.AreNotEqual(string.Empty, model.Message);
    }

    [TestMethod]
    public async Task SendEmailCommand_FriendshipRemovedOrRoomFull_DoesNotSendAsync()
    {
        // Arrange
        var client = new StubRoomInvitationsClient();
        var context = new RoomInvitationsNavigationContext(42) { InvitationRecipientUserId = 7 };
        context.Friends.Add(new FriendItemViewModel(7, "Friend", false));
        using RoomInvitationsViewModel model = Create(client, context);

        // Act
        model.IsRoomAvailable = false;
        await CommandExecution.ExecuteAsync(model.SendEmailCommand, null);
        model.IsRoomAvailable = true;
        context.Friends.Clear();
        await CommandExecution.ExecuteAsync(model.SendEmailCommand, null);

        // Assert
        Assert.AreEqual(0, client.SendCount);
        Assert.IsFalse(model.CanSendEmail);
    }

    [TestMethod]
    public async Task SendEmailCommand_DisposedDuringDelivery_DiscardsLateResponseAsync()
    {
        // Arrange
        var completion = new TaskCompletionSource<SendRoomInvitationResponse>();
        var client = new StubRoomInvitationsClient { SendHandler = () => completion.Task };
        var context = new RoomInvitationsNavigationContext(42) { InvitationRecipientUserId = 7 };
        context.Friends.Add(new FriendItemViewModel(7, "Friend", false));
        using RoomInvitationsViewModel model = Create(client, context);

        // Act
        Task sending = CommandExecution.ExecuteAsync(model.SendEmailCommand, null);
        model.Dispose();
        completion.SetResult(new SendRoomInvitationResponse
        {
            DeliveryStatus = RoomInvitationDeliveryStatus.EmailAccepted
        });
        await sending;

        // Assert
        Assert.AreEqual(string.Empty, model.Message);
        Assert.IsFalse(model.CanSendEmail);
        Assert.AreEqual(1, client.SendCount);
    }

    [TestMethod]
    [DataRow(RoomInvitationDeliveryStatus.Unspecified)]
    [DataRow(RoomInvitationDeliveryStatus.InApp)]
    public async Task SendEmailCommand_LegacyServer_DoesNotClaimEmailDeliveryAsync(
        RoomInvitationDeliveryStatus status)
    {
        // Arrange
        var client = new StubRoomInvitationsClient
        {
            SendHandler = () => Task.FromResult(new SendRoomInvitationResponse
            {
                DeliveryStatus = status
            })
        };
        var context = new RoomInvitationsNavigationContext(42) { InvitationRecipientUserId = 7 };
        context.Friends.Add(new FriendItemViewModel(7, "Friend", false));
        using RoomInvitationsViewModel model = Create(client, context);

        // Act
        await CommandExecution.ExecuteAsync(model.SendEmailCommand, null);

        // Assert
        Assert.AreEqual(Properties.Resources.ResourceManager.GetString(
            "RoomInvitations_Err_EmailUnconfirmed",
            System.Globalization.CultureInfo.CurrentUICulture),
            model.Message);
    }

    [TestMethod]
    public async Task SendCommand_FriendRow_SendsOnlyTheClickedConnectedFriendAsync()
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
    public async Task OnlineFriends_PresenceAndMembershipChanges_UpdatesWithoutManualRefreshAsync()
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
    public async Task SendCommand_FullRoom_DoesNotInviteAsync()
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
    public async Task AcceptCommand_ConfirmedResponse_OpensPrivateLobbyAsync()
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
            new RoomInvitationsNavigationContext(0))
        {
            IsAcceptanceAllowed = true
        };
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
    public async Task AcceptCommand_PlayerBusyOrInvitationExpired_DoesNotAcceptAsync()
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
    public async Task RejectCommand_SelectedInvitation_SendsDecisionAsync()
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
    public async Task SendCommand_TransportFailure_DoesNotReplayMutationAsync()
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
    public async Task Dispose_PendingAcceptance_DoesNotNavigateAfterLateResponseAsync()
    {
        // Arrange
        StubRoomInvitationsClient client = IncomingClient();
        var completion = new TaskCompletionSource<RespondRoomInvitationResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        client.RespondHandler = () => completion.Task;
        INavigationService navigation =
            DispatchProxy.Create<INavigationService, RecordingNavigationProxy>();
        using var model = new RoomInvitationsViewModel(navigation, client,
            new RoomInvitationsNavigationContext(0))
        {
            IsAcceptanceAllowed = true
        };
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
    public async Task
        RequestRefresh_NotificationDuringLoad_ReconcilesLatestStateAutomaticallyAsync()
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
            InvitationId = 7,
            RoomId = 42,
            RoomCode = "ROOM12",
            SenderName = "Host",
            RecipientName = "Friend",
            State = RoomInvitationState.Pending
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
