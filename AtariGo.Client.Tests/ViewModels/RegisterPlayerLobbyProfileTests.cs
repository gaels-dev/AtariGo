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

    private sealed class EmptyLeaderboardClient : ILeaderboardClient
    {
        public Task<GetTopPlayersResponse> GetTopPlayersAsync(
            int limit, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new GetTopPlayersResponse());
        }
    }
}
