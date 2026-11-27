using System.Reflection;

using AtariGo.Client.Models;
using AtariGo.Client.Services;
using AtariGo.Client.Tests.Services;
using AtariGo.Client.ViewModels.Dialogs;

namespace AtariGo.Client.Tests.ViewModels;

[TestClass]
[TestCategory("Unit")]
public sealed class PasswordRecoveryDialogViewModelTests
{
    [TestMethod]
    public async Task ResetCommand_UnconfirmedResult_ClearsSecretsAndPreventsRetryAsync()
    {
        var client = new StubPasswordRecoveryClient
        {
            Response = Task.FromResult(PasswordRecoveryResult.Unconfirmed)
        };
        INavigationService navigation = DispatchProxy.Create<INavigationService,
            RecordingNavigationProxy>();
        using var model = new PasswordRecoveryDialogViewModel(client, navigation);
        model.Identifier = "Owner";
        await CommandExecution.ExecuteAsync(model.RequestCommand, null);
        model.Code = "123456";
        PasswordRecoveryInput input = CreateInput();
        await CommandExecution.ExecuteAsync(model.ResetCommand, input);
        await CommandExecution.ExecuteAsync(model.ResetCommand, CreateInput());
        Assert.IsFalse(model.CanReset);
        Assert.IsFalse(model.CanEdit);
        Assert.AreEqual(1, client.ResetCalls);
        Assert.AreEqual(string.Empty, input.NewPassword);
        Assert.AreEqual(string.Empty, input.Confirmation);
        Assert.AreEqual(string.Empty, input.Code);
        Assert.AreEqual(string.Empty, model.Code);
        Assert.IsFalse(string.IsNullOrWhiteSpace(model.StatusMessage));
        Assert.AreEqual(0, ((RecordingNavigationProxy)navigation).Calls.Count);
    }

    [TestMethod]
    public async Task CancelCommand_PendingReset_CancelsAndDiscardsLateSuccessAsync()
    {
        var pending = new TaskCompletionSource<PasswordRecoveryResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new StubPasswordRecoveryClient { Response = pending.Task };
        INavigationService navigation = DispatchProxy.Create<INavigationService,
            RecordingNavigationProxy>();
        using var model = new PasswordRecoveryDialogViewModel(client, navigation);
        model.Identifier = "Owner";
        await CommandExecution.ExecuteAsync(model.RequestCommand, null);
        model.Code = "123456";
        PasswordRecoveryInput input = CreateInput();
        Task command = CommandExecution.ExecuteAsync(model.ResetCommand, input);
        string previous = model.StatusMessage;
        model.CancelCommand.Execute(null);
        pending.SetResult(PasswordRecoveryResult.Reset);
        await command;
        Assert.IsTrue(client.RequestToken.IsCancellationRequested);
        Assert.AreEqual(previous, model.StatusMessage);
        Assert.AreEqual(string.Empty, model.Code);
        Assert.AreEqual(string.Empty, model.Identifier);
        Assert.AreEqual(string.Empty, input.NewPassword);
        CollectionAssert.AreEqual(new[] { "CloseLoginDialog" },
            ((RecordingNavigationProxy)navigation).Calls.ToArray());
    }

    public static PasswordRecoveryInput CreateInput()
    {
        return new PasswordRecoveryInput
        {
            Identifier = "Owner",
            Code = "123456",
            NewPassword = " NewPassword9 ",
            Confirmation = " NewPassword9 "
        };
    }
}
