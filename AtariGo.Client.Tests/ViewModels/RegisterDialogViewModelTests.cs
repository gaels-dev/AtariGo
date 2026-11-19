using System.Reflection;

using Grpc.Core;

using AtariGo.Client.Services;
using AtariGo.Client.Tests.Services;
using AtariGo.Client.ViewModels.Dialogs;
using AtariGo.Contracts;

namespace AtariGo.Client.Tests.ViewModels;

[TestClass]
[TestCategory("Unit")]
public sealed class RegisterDialogViewModelTests
{
    [TestMethod]
    [DataRow("", "player@example.com", "Register_Err_InvalidUsername")]
    [DataRow("Guest", "player@example.com", "Register_Err_ReservedUsername")]
    [DataRow("guest-123", "player@example.com", "Register_Err_ReservedUsername")]
    [DataRow("a@b", "player@example.com", "Register_Err_UsernameAtSign")]
    [DataRow("Player One", "player@example.com", "Register_Err_InvalidUsername")]
    [DataRow("Player", "", "Register_Err_InvalidEmail")]
    [DataRow("Player", "Player <player@example.com>", "Register_Err_InvalidEmail")]
    [DataRow("Player", "player@local", "Register_Err_InvalidEmail")]
    public async Task RegisterCommand_InvalidIdentity_ShowsSpecificErrorWithoutRequestAsync(
        string name, string email, string expectedKey)
    {
        INavigationService navigation = CreateNavigation();
        StubRegistrationClient client = CreateClient();
        using RegisterDialogViewModel model = CreateValidModel(navigation, client);
        model.UserName = name;
        model.Email = email;

        await CommandExecution.ExecuteAsync(model.RegisterCommand, null);

        Assert.AreEqual(AuthenticationMessages.GetText(expectedKey), model.ErrorMessage);
        Assert.AreEqual(0, client.Calls);
        Assert.AreEqual(0, ((RecordingNavigationProxy)navigation).Calls.Count);
    }

    [TestMethod]
    public async Task RegisterCommand_ValidFields_CreatesAccountDirectlyAndClearsSecretsAsync()
    {
        INavigationService navigation = CreateNavigation();
        StubRegistrationClient client = CreateClient();
        using RegisterDialogViewModel model = CreateValidModel(navigation, client);
        model.UserName = "  Muñoz_1  ";
        model.Email = "  player+go@example.com  ";

        await CommandExecution.ExecuteAsync(model.RegisterCommand, null);

        Assert.AreEqual(1, client.Calls);
        Assert.AreEqual("Muñoz_1", client.LastDraft?.UserName);
        Assert.AreEqual("player+go@example.com", client.LastDraft?.Email);
        Assert.AreEqual(string.Empty, client.LastDraft?.Password);
        Assert.AreEqual(string.Empty, model.Password);
        Assert.AreEqual(string.Empty, model.PasswordConfirmation);
        CollectionAssert.AreEqual(new[] { nameof(INavigationService.ShowRegistrationSuccess) },
            ((RecordingNavigationProxy)navigation).Calls.ToArray());
    }

    [TestMethod]
    [DataRow(RegistrationResult.UsernameTaken, "RegistrationResult_Msg_UsernameTaken")]
    [DataRow(RegistrationResult.EmailTaken, "RegistrationResult_Msg_EmailTaken")]
    [DataRow(RegistrationResult.Error, "RegistrationResult_Msg_Error")]
    [DataRow((RegistrationResult)999, "RegistrationResult_Msg_Error")]
    public async Task RegisterCommand_RejectedResult_KeepsFormWithoutReportingSuccessAsync(
        RegistrationResult result, string key)
    {
        INavigationService navigation = CreateNavigation();
        var client = new StubRegistrationClient(_ => Task.FromResult(
            new RegisterAccountResponse { Result = result }));
        using RegisterDialogViewModel model = CreateValidModel(navigation, client);

        await CommandExecution.ExecuteAsync(model.RegisterCommand, null);

        Assert.AreEqual(AuthenticationMessages.GetText(key), model.ErrorMessage);
        Assert.AreEqual(0, ((RecordingNavigationProxy)navigation).Calls.Count);
        Assert.IsTrue(model.CanEdit);
    }

    [TestMethod]
    [DataRow(StatusCode.DeadlineExceeded)]
    [DataRow(StatusCode.Unavailable)]
    public async Task RegisterCommand_LostResponse_DoesNotReplayRegistrationAsync(StatusCode status)
    {
        INavigationService navigation = CreateNavigation();
        var client = new StubRegistrationClient(_ => Task.FromException<RegisterAccountResponse>(
            new RpcException(new Status(status, "Lost response"))));
        using RegisterDialogViewModel model = CreateValidModel(navigation, client);

        await CommandExecution.ExecuteAsync(model.RegisterCommand, null);

        Assert.AreEqual(1, client.Calls);
        Assert.AreEqual(AuthenticationMessages.GetText("Registration_Err_Unconfirmed"),
            model.ErrorMessage);
        Assert.AreEqual(0, ((RecordingNavigationProxy)navigation).Calls.Count);
        Assert.AreEqual(string.Empty, model.Password);
    }

    [TestMethod]
    public async Task CancelCommand_LateSuccessfulResponse_DiscardsResultAsync()
    {
        INavigationService navigation = CreateNavigation();
        var pending = new TaskCompletionSource<RegisterAccountResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken captured = default;
        var client = new StubRegistrationClient(token =>
        {
            captured = token;
            return pending.Task;
        });
        using RegisterDialogViewModel model = CreateValidModel(navigation, client);
        Task command = CommandExecution.ExecuteAsync(model.RegisterCommand, null);

        model.CancelCommand.Execute(null);
        model.Dispose();
        pending.SetResult(new RegisterAccountResponse { Result = RegistrationResult.Success });
        await command;

        Assert.IsTrue(captured.IsCancellationRequested);
        Assert.AreEqual(string.Empty, model.Password);
        CollectionAssert.AreEqual(new[] { nameof(INavigationService.CloseLoginDialog) },
            ((RecordingNavigationProxy)navigation).Calls.ToArray());
    }

    [TestMethod]
    [DataRow("", "", "Register_Err_PasswordPolicy")]
    [DataRow("Short1", "Short1", "Register_Err_PasswordPolicy")]
    [DataRow("lowercase9", "lowercase9", "Register_Err_PasswordPolicy")]
    [DataRow("Password9", "", "Register_Err_PasswordMismatch")]
    [DataRow("Password9", "password9", "Register_Err_PasswordMismatch")]
    [DataRow(" Password9 ", "Password9", "Register_Err_PasswordMismatch")]
    public async Task RegisterCommand_InvalidPassword_DoesNotSubmitAsync(
        string password, string confirmation, string expectedKey)
    {
        INavigationService navigation = CreateNavigation();
        StubRegistrationClient client = CreateClient();
        using RegisterDialogViewModel model = CreateValidModel(navigation, client);
        model.Password = password;
        model.PasswordConfirmation = confirmation;

        await CommandExecution.ExecuteAsync(model.RegisterCommand, null);

        string expected = expectedKey == "Register_Err_PasswordPolicy"
            ? AuthenticationMessages.GetPasswordPolicy()
            : AuthenticationMessages.GetText(expectedKey);
        Assert.AreEqual(expected, model.ErrorMessage);
        Assert.AreEqual(0, client.Calls);
    }

    private static INavigationService CreateNavigation()
    {
        return DispatchProxy.Create<INavigationService, RecordingNavigationProxy>();
    }

    private static StubRegistrationClient CreateClient()
    {
        return new StubRegistrationClient(_ => Task.FromResult(
            new RegisterAccountResponse { Result = RegistrationResult.Success }));
    }

    private static RegisterDialogViewModel CreateValidModel(INavigationService navigation,
        IRegistrationClient client)
    {
        return new RegisterDialogViewModel(navigation, client)
        {
            UserName = "Player",
            Email = "player@example.com",
            Password = "ValidPassword",
            PasswordConfirmation = "ValidPassword"
        };
    }
}
