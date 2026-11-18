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
    public async Task LoadAndUpdate_ValidProfile_ConfirmsPlayerNameAndAvatarAsync()
    {
        // Arrange
        INavigationService navigation = DispatchProxy.Create<INavigationService,
            RecordingNavigationProxy>();
        StubProfileClient client = new StubProfileClient();
        UserProfile? confirmedProfile = null;
        using ProfileCustomizationViewModel model = new ProfileCustomizationViewModel(
            navigation, client, profile => confirmedProfile = profile);

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
        Assert.IsNotNull(confirmedProfile);
        Assert.AreEqual("NewName", confirmedProfile.UserName);
        Assert.AreEqual(4, confirmedProfile.AvatarId);
        Assert.IsNotNull(client.LastRequest);
        Assert.AreEqual("NewName", client.LastRequest.UserName);
        Assert.AreEqual(4, client.LastRequest.AvatarId);
        Assert.IsFalse(string.IsNullOrWhiteSpace(model.StatusMessage));
    }

    [TestMethod]
    public async Task UpdateProfileCommand_RejectedUpdate_PreservesConfirmedNameAsync()
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
        List<UserProfile> confirmedProfiles = new List<UserProfile>();
        using ProfileCustomizationViewModel model = new ProfileCustomizationViewModel(
            navigation, client, profile => confirmedProfiles.Add(profile));
        await CommandExecution.ExecuteAsync(model.LoadCommand, null);
        model.PlayerName = "PlayerTwo";

        // Act
        await CommandExecution.ExecuteAsync(model.UpdateProfileCommand, null);

        // Assert
        Assert.HasCount(1, confirmedProfiles);
        Assert.AreEqual("PlayerOne", confirmedProfiles[0].UserName);
        Assert.AreEqual(1, confirmedProfiles[0].AvatarId);
        Assert.AreEqual("PlayerTwo", model.PlayerName);
        Assert.IsFalse(string.IsNullOrWhiteSpace(model.StatusMessage));
    }

    [TestMethod]
    public async Task AvatarPicker_CompletedWins_ControlsTwentyAvatarChoicesAsync()
    {
        // Arrange
        INavigationService navigation = DispatchProxy.Create<INavigationService,
            RecordingNavigationProxy>();
        StubProfileClient client = new StubProfileClient
        {
            Profile = new UserProfile
            {
                UserName = "PlayerOne",
                Email = "first@example.test",
                Wins = 9,
                AvatarId = 1
            }
        };
        using ProfileCustomizationViewModel model = new ProfileCustomizationViewModel(
            navigation, client, IgnoreProfile);

        // Act
        await CommandExecution.ExecuteAsync(model.LoadCommand, null);
        model.OpenAvatarPickerCommand.Execute(null);
        AvatarOptionViewModel locked = model.AvailableAvatars.Single(option =>
            option.AvatarId == 7);
        model.SelectAvatarCommand.Execute(locked);
        AvatarOptionViewModel unlocked = model.AvailableAvatars.Single(option =>
            option.AvatarId == 6);
        model.SelectAvatarCommand.Execute(unlocked);

        // Assert
        Assert.HasCount(20, model.AvailableAvatars);
        Assert.HasCount(4, model.FeaturedAvatars);
        Assert.AreEqual(5, unlocked.RequiredWins);
        Assert.AreEqual(10, locked.RequiredWins);
        Assert.IsTrue(locked.IsLocked);
        Assert.AreEqual(75, model.AvailableAvatars[19].RequiredWins);
        Assert.AreEqual(6, model.SelectedAvatarId);
        Assert.IsTrue(unlocked.IsSelected);
        Assert.IsFalse(model.IsAvatarPickerOpen);
    }

    private static void IgnoreProfile(UserProfile profile)
    {
    }
}
