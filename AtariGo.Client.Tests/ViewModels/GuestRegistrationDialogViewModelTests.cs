using System.Reflection;

using AtariGo.Client.Services;
using AtariGo.Client.Tests.Services;
using AtariGo.Client.ViewModels.Dialogs;

namespace AtariGo.Client.Tests.ViewModels;

[TestClass]
[TestCategory("Unit")]
public sealed class GuestRegistrationDialogViewModelTests
{
    [TestMethod]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow("Player@")]
    [DataRow("Player One")]
    [DataRow("Player\u200b")]
    [DataRow("Player\t")]
    [DataRow("1234567890123456")]
    public async Task EnterCommand_InvalidName_DoesNotCreateIdentityAsync(string name)
    {
        INavigationService navigation = CreateNavigation();
        var client = new StubGuestIdentityClient();
        using var model = new GuestRegistrationDialogViewModel(navigation, client);
        model.GuestName = name;

        await CommandExecution.ExecuteAsync(model.EnterCommand, null);

        Assert.AreEqual(0, client.Calls);
        Assert.IsFalse(string.IsNullOrWhiteSpace(model.ErrorMessage));
        Assert.AreEqual(0, ((RecordingNavigationProxy)navigation).Calls.Count);
    }

    [TestMethod]
    [DataRow(" Muñoz_1 ", "Guest-Muñoz_1")]
    [DataRow("123456789012345", "Guest-123456789012345")]
    public async Task EnterCommand_ValidName_OpensGuestSessionAsync(string name, string displayName)
    {
        INavigationService navigation = CreateNavigation();
        var client = new StubGuestIdentityClient();
        using var model = new GuestRegistrationDialogViewModel(navigation, client);
        model.GuestName = name;

        await CommandExecution.ExecuteAsync(model.EnterCommand, null);

        Assert.AreEqual(displayName, client.DisplayName);
        CollectionAssert.AreEqual(new[] { nameof(INavigationService.NavigateToMainWindow) },
            ((RecordingNavigationProxy)navigation).Calls.ToArray());
    }

    [TestMethod]
    public async Task Dispose_LateIdentity_RevokesCredentialWithoutNavigatingAsync()
    {
        INavigationService navigation = CreateNavigation();
        var pending = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new StubGuestIdentityClient { Response = pending.Task };
        using var model = new GuestRegistrationDialogViewModel(navigation, client);
        model.GuestName = "Player";
        Task command = CommandExecution.ExecuteAsync(model.EnterCommand, null);

        model.Dispose();
        model.Dispose();
        pending.SetResult("late-credential");
        await command;

        Assert.IsTrue(client.RequestToken.IsCancellationRequested);
        Assert.AreEqual("late-credential", client.RevokedCredential);
        Assert.AreEqual(0, ((RecordingNavigationProxy)navigation).Calls.Count);
    }

    private static INavigationService CreateNavigation()
    {
        return DispatchProxy.Create<INavigationService, RecordingNavigationProxy>();
    }
}
