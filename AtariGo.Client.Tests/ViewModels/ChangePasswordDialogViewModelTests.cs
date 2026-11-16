using AtariGo.Client.Models;
using AtariGo.Client.Tests.Services;
using AtariGo.Client.ViewModels.Dialogs;

namespace AtariGo.Client.Tests.ViewModels;

[TestClass]
[TestCategory("Unit")]
public sealed class ChangePasswordDialogViewModelTests
{
    [TestMethod]
    public async Task SaveCommand_ValidationAndUnconfirmedResult_ClearsSecretsWithoutRetry()
    {
        // Arrange
        var client = new StubCredentialsClient();
        using var model = new ChangePasswordDialogViewModel(client);
        var invalid = new PasswordChangeInput
        {
            CurrentPassword = "OldPassword8", NewPassword = "short", Confirmation = "short"
        };
        int clears = 0;
        model.PasswordsCleared += () => clears++;
        // Act
        await CommandExecution.ExecuteAsync(model.SaveCommand, invalid);
        Assert.AreEqual(0, client.Calls);
        client.Response = Task.FromResult(PasswordChangeResult.Unconfirmed);
        PasswordChangeInput input = CreateInput();
        await CommandExecution.ExecuteAsync(model.SaveCommand, input);
        // Assert
        Assert.AreEqual(1, client.Calls);
        Assert.IsFalse(model.HasSucceeded);
        Assert.IsFalse(model.IsSaving);
        Assert.IsTrue(model.CanEdit);
        Assert.AreEqual(string.Empty, input.CurrentPassword);
        Assert.AreEqual(string.Empty, input.NewPassword);
        Assert.AreEqual(string.Empty, input.Confirmation);
        Assert.AreEqual(1, clears);
        Assert.IsFalse(string.IsNullOrWhiteSpace(model.StatusMessage));
    }

    [TestMethod]
    public async Task Dispose_PendingChange_CancelsAndIgnoresLateSuccess()
    {
        // Arrange
        var pending = new TaskCompletionSource<PasswordChangeResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new StubCredentialsClient { Response = pending.Task };
        using var model = new ChangePasswordDialogViewModel(client);
        PasswordChangeInput input = CreateInput();
        Task command = CommandExecution.ExecuteAsync(model.SaveCommand, input);
        string previousMessage = model.StatusMessage;
        bool closed = false;
        model.DialogClosed += _ => closed = true;
        // Act
        model.DismissCommand.Execute(null);
        Assert.IsFalse(closed);
        model.Dispose();
        pending.SetResult(PasswordChangeResult.Changed);
        await command;
        // Assert
        Assert.IsTrue(client.RequestToken.IsCancellationRequested);
        Assert.IsFalse(model.HasSucceeded);
        Assert.AreEqual(previousMessage, model.StatusMessage);
        Assert.AreEqual(string.Empty, input.CurrentPassword);
        Assert.AreEqual(1, client.Calls);
    }

    public static PasswordChangeInput CreateInput()
    {
        return new PasswordChangeInput
        {
            CurrentPassword = "OldPassword8", NewPassword = " NewPassword9 ",
            Confirmation = " NewPassword9 "
        };
    }
}
