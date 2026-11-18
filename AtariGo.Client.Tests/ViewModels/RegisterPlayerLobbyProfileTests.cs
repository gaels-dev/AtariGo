using System.Reflection;

using AtariGo.Client.Services;
using AtariGo.Client.Tests.Services;
using AtariGo.Client.ViewModels;
using AtariGo.Contracts;

namespace AtariGo.Client.Tests.ViewModels;

[TestClass]
[TestCategory("Unit")]
public sealed class RegisterPlayerLobbyProfileTests
{
    [TestMethod]
    public void Constructor_LoadsWinsAndAvatarFromProfile()
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
        using RegisterPlayerLobbyViewModel model = new RegisterPlayerLobbyViewModel(
            navigation, new EmptyLeaderboardClient(), context);

        // Assert
        Assert.AreEqual(7, model.PlayerWins);
        Assert.AreEqual(3, model.PlayerAvatarId);
    }

    [TestMethod]
    public async Task Constructor_UsesCachedAvatarUntilProfileArrives()
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
        using RegisterPlayerLobbyViewModel model = new RegisterPlayerLobbyViewModel(
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
    public async Task Constructor_WithoutCachedAvatarWaitsForProfile()
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
        using RegisterPlayerLobbyViewModel model = new RegisterPlayerLobbyViewModel(
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
