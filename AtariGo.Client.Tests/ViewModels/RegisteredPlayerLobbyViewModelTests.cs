using System.Reflection;

using AtariGo.Client.Services;
using AtariGo.Client.Tests.Services;
using AtariGo.Client.ViewModels;
using AtariGo.Contracts;

namespace AtariGo.Client.Tests.ViewModels;

[TestClass]
[TestCategory("Unit")]
public sealed class RegisteredPlayerLobbyViewModelTests
{
    [TestMethod]
    public void InviteFriendCommand_ConnectedFriend_OpensCreationWithRecipient()
    {
        // Arrange
        INavigationService navigation =
            DispatchProxy.Create<INavigationService, RecordingNavigationProxy>();
        var context = new RegisteredLobbyContext();
        var friend = new FriendItemViewModel(7, "Friend", true);
        var unidentified = new FriendItemViewModel("Unknown", true);
        context.Friends.Add(friend);
        context.Friends.Add(unidentified);
        using var model = new RegisteredPlayerLobbyViewModel(
            navigation, new EmptyLeaderboardClient(), context);

        // Act
        model.InviteFriendCommand.Execute(friend);
        friend.IsOnline = false;
        model.InviteFriendCommand.Execute(friend);
        model.InviteFriendCommand.Execute(new FriendItemViewModel(8, "Other", true));
        model.InviteFriendCommand.Execute(unidentified);
        model.InviteFriendCommand.Execute(null);

        // Assert
        RecordingNavigationProxy recorder = (RecordingNavigationProxy)navigation;
        CollectionAssert.AreEqual(new[]
        {
            nameof(INavigationService.NavigateToCreatePrivateRoomForInvitation)
        }, recorder.Calls.ToArray());
        Assert.AreEqual(7, recorder.LastNavigationArgument);
    }

    [TestMethod]
    public void InviteFriendByEmailCommand_OfflineFriend_OpensCreationWithRecipient()
    {
        // Arrange
        INavigationService navigation =
            DispatchProxy.Create<INavigationService, RecordingNavigationProxy>();
        var context = new RegisteredLobbyContext();
        var leaderboard = new EmptyLeaderboardClient();
        var friend = new FriendItemViewModel(7, "Friend", false);
        context.Friends.Add(friend);
        using var model = new RegisteredPlayerLobbyViewModel(
            navigation, leaderboard, context);

        // Act
        model.InviteFriendByEmailCommand.Execute(friend);
        friend.IsOnline = true;
        model.InviteFriendByEmailCommand.Execute(friend);
        model.InviteFriendByEmailCommand.Execute(new FriendItemViewModel(8, "Other", false));

        // Assert
        RecordingNavigationProxy recorder = (RecordingNavigationProxy)navigation;
        CollectionAssert.AreEqual(new[]
        {
            nameof(INavigationService.NavigateToCreatePrivateRoomForInvitation)
        }, recorder.Calls.ToArray());
        Assert.AreEqual(7, recorder.LastNavigationArgument);
    }

    [TestMethod]
    public void Constructor_AvailableProfile_LoadsWinsAndAvatar()
    {
        // Arrange
        INavigationService navigation = DispatchProxy.Create<INavigationService,
            RecordingNavigationProxy>();
        StubProfileClient profileClient = new StubProfileClient
        {
            Profile = new UserProfile
            {
                UserName = "PlayerOne",
                Email = "first@example.test",
                Wins = 7,
                AvatarId = 3
            }
        };
        RegisteredLobbyContext context = new RegisteredLobbyContext
        {
            ProfileClient = profileClient
        };

        // Act
        using RegisteredPlayerLobbyViewModel model = new RegisteredPlayerLobbyViewModel(
            navigation, new EmptyLeaderboardClient(), context);

        // Assert
        Assert.AreEqual(7, model.PlayerWins);
        Assert.AreEqual(3, model.PlayerAvatarId);
    }

    [TestMethod]
    public async Task Constructor_PendingProfile_UsesCachedAvatarAsync()
    {
        // Arrange
        INavigationService navigation = DispatchProxy.Create<INavigationService,
            RecordingNavigationProxy>();
        TaskCompletionSource<UserProfile> pendingProfile = new TaskCompletionSource<UserProfile>();
        TaskCompletionSource<int> loadedAvatar = new TaskCompletionSource<int>();
        StubProfileClient profileClient = new StubProfileClient
        {
            ProfileResponse = pendingProfile.Task
        };
        RegisteredLobbyContext context = new RegisteredLobbyContext
        {
            ProfileClient = profileClient,
            InitialAvatarId = 4,
            AvatarLoaded = avatarId => loadedAvatar.SetResult(avatarId)
        };

        // Act
        using RegisteredPlayerLobbyViewModel model = new RegisteredPlayerLobbyViewModel(
            navigation, new EmptyLeaderboardClient(), context);
        int? initialAvatarId = model.PlayerAvatarId;
        bool initiallyVisible = model.HasPlayerAvatar;
        pendingProfile.SetResult(new UserProfile { Wins = 7, AvatarId = 6 });
        int confirmedAvatarId = await loadedAvatar.Task.WaitAsync(TimeSpan.FromSeconds(2));

        // Assert
        Assert.AreEqual(4, initialAvatarId);
        Assert.IsTrue(initiallyVisible);
        Assert.AreEqual(6, confirmedAvatarId);
        Assert.AreEqual(6, model.PlayerAvatarId);
        Assert.IsTrue(model.HasPlayerAvatar);
        Assert.AreEqual(7, model.PlayerWins);
    }

    [TestMethod]
    public async Task Constructor_MissingCachedAvatar_WaitsForProfileAsync()
    {
        // Arrange
        INavigationService navigation = DispatchProxy.Create<INavigationService,
            RecordingNavigationProxy>();
        TaskCompletionSource<UserProfile> pendingProfile = new TaskCompletionSource<UserProfile>();
        TaskCompletionSource<int> loadedAvatar = new TaskCompletionSource<int>();
        StubProfileClient profileClient = new StubProfileClient
        {
            ProfileResponse = pendingProfile.Task
        };
        RegisteredLobbyContext context = new RegisteredLobbyContext
        {
            ProfileClient = profileClient,
            AvatarLoaded = avatarId => loadedAvatar.SetResult(avatarId)
        };

        // Act
        using RegisteredPlayerLobbyViewModel model = new RegisteredPlayerLobbyViewModel(
            navigation, new EmptyLeaderboardClient(), context);
        bool initiallyVisible = model.HasPlayerAvatar;
        pendingProfile.SetResult(new UserProfile { Wins = 3, AvatarId = 2 });
        await loadedAvatar.Task.WaitAsync(TimeSpan.FromSeconds(2));

        // Assert
        Assert.IsFalse(initiallyVisible);
        Assert.AreEqual(2, model.PlayerAvatarId);
        Assert.IsTrue(model.HasPlayerAvatar);
    }

    private sealed class EmptyLeaderboardClient : ILeaderboardClient
    {
        public Task<GetTopPlayersResponse> GetTopPlayersAsync(
            int limit, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new GetTopPlayersResponse());
        }
    }
}
