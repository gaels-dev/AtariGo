using System.Reflection;

using AtariGo.Client.Services;
using AtariGo.Client.Tests.Services;
using AtariGo.Client.ViewModels;
using AtariGo.Contracts;

namespace AtariGo.Client.Tests.ViewModels;

[TestClass]
[TestCategory("Unit")]
public sealed class ProfileCustomizationViewModelTests
{
    [TestMethod]
    public async Task LoadAndUpdate_UsesServerProfileAndRefreshesPlayerName()
    {
        // Arrange
        INavigationService navigation = DispatchProxy.Create<INavigationService,
            RecordingNavigationProxy>();
        StubProfileClient client = new StubProfileClient();
        string? updatedName = null;
        using ProfileCustomizationViewModel model = new ProfileCustomizationViewModel(
            navigation, client, name => updatedName = name);

        // Act
        await CommandExecution.ExecuteAsync(model.LoadCommand, null);
        model.PlayerName = "NewName";
        model.SelectedAvatarId = 4;
        await CommandExecution.ExecuteAsync(model.UpdateProfileCommand, null);

        // Assert
        Assert.AreEqual("first@example.test", model.Email);
        Assert.AreEqual(3, model.Wins);
        Assert.AreEqual("NewName", model.PlayerName);
        Assert.AreEqual(4, model.SelectedAvatarId);
        Assert.AreEqual("NewName", updatedName);
        Assert.IsNotNull(client.LastRequest);
        Assert.AreEqual("NewName", client.LastRequest.UserName);
        Assert.AreEqual(4, client.LastRequest.AvatarId);
        Assert.IsFalse(string.IsNullOrWhiteSpace(model.StatusMessage));
    }

    [TestMethod]
    public async Task RejectedUpdate_LeavesConfirmedNameUnchanged()
    {
        // Arrange
        INavigationService navigation = DispatchProxy.Create<INavigationService,
            RecordingNavigationProxy>();
        StubProfileClient client = new StubProfileClient
        {
            UpdateResponse = new UpdateProfileResponse
            {
                Status = ProfileUpdateStatus.UserNameTaken
            }
        };
        string? updatedName = null;
        using ProfileCustomizationViewModel model = new ProfileCustomizationViewModel(
            navigation, client, name => updatedName = name);
        await CommandExecution.ExecuteAsync(model.LoadCommand, null);
        model.PlayerName = "PlayerTwo";

        // Act
        await CommandExecution.ExecuteAsync(model.UpdateProfileCommand, null);

        // Assert
        Assert.IsNull(updatedName);
        Assert.AreEqual("PlayerTwo", model.PlayerName);
        Assert.IsFalse(string.IsNullOrWhiteSpace(model.StatusMessage));
    }
}
