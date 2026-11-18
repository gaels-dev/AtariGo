using AtariGo.Client.Models;
using AtariGo.Client.Tests.Services;
using AtariGo.Client.ViewModels.Dialogs;

namespace AtariGo.Client.Tests.ViewModels;

[TestClass]
[TestCategory("Unit")]
public sealed class LoginVerificationDialogViewModelTests
{
    [TestMethod]
    public async Task ConfirmCommand_UnconfirmedResult_ClearsCodeAndNeverRetriesAutomatically()
    {
        StubAuthenticationClient client = CreateClient();
        client.Verify = _ => Task.FromResult(new LoginAttemptResult
        {
            Status = LoginAttemptStatus.Unconfirmed
        });
        int completions = 0;
        using var model = new LoginVerificationDialogViewModel(client, Pending(),
            _ => completions++);
        model.Code = "01234567";
        await CommandExecution.ExecuteAsync(model.ConfirmCommand, null);
        await CommandExecution.ExecuteAsync(model.ConfirmCommand, null);
        Assert.AreEqual(1, client.VerificationCalls);
        Assert.AreEqual(string.Empty, model.Code);
        Assert.IsFalse(model.CanEdit);
        Assert.AreEqual(0, completions);
    }

    [TestMethod]
    public async Task Dispose_PendingConfirmation_CancelsAndDiscardsLateSuccess()
    {
        var response = new TaskCompletionSource<LoginAttemptResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        StubAuthenticationClient client = CreateClient();
        CancellationToken captured = default;
        client.Verify = token =>
        {
            captured = token;
            return response.Task;
        };
        int completions = 0;
        using var model = new LoginVerificationDialogViewModel(client, Pending(),
            _ => completions++);
        model.Code = "01234567";
        Task running = CommandExecution.ExecuteAsync(model.ConfirmCommand, null);
        model.Dispose();
        response.SetResult(new LoginAttemptResult
        {
            Status = LoginAttemptStatus.Success, SessionToken = "session"
        });
        await running;
        Assert.IsTrue(captured.IsCancellationRequested);
        Assert.AreEqual(0, completions);
        Assert.AreEqual(string.Empty, model.Code);
    }

    [TestMethod]
    public async Task CancelCommand_ValidAttempt_ClosesDialogAndRequestsServerCancellation()
    {
        StubAuthenticationClient client = CreateClient();
        int completions = 0;
        using var model = new LoginVerificationDialogViewModel(client, Pending(),
            _ => completions++);
        model.Code = "01234567";
        await CommandExecution.ExecuteAsync(model.CancelCommand, null);
        Assert.AreEqual(1, completions);
        Assert.AreEqual(1, client.CancellationCalls);
        Assert.AreEqual(string.Empty, model.Code);
        Assert.IsFalse(model.CanEdit);
    }

    [TestMethod]
    public async Task ConfirmCommand_InvalidCode_DoesNotSendRequest()
    {
        StubAuthenticationClient client = CreateClient();
        using var model = new LoginVerificationDialogViewModel(client, Pending(), _ => { });
        model.Code = "bad";
        await CommandExecution.ExecuteAsync(model.ConfirmCommand, null);
        Assert.AreEqual(0, client.VerificationCalls);
        Assert.IsTrue(model.CanEdit);
    }

    [TestMethod]
    public async Task ResendCommand_UnconfirmedDelivery_AllowsEnteringCodeWithoutAutomaticRetry()
    {
        StubAuthenticationClient client = CreateClient();
        client.Resend = _ => Task.FromResult(new LoginAttemptResult
        {
            Status = LoginAttemptStatus.Unconfirmed
        });
        using var model = new LoginVerificationDialogViewModel(client, Pending(), _ => { });
        await CommandExecution.ExecuteAsync(model.ResendCommand, null);
        Assert.IsTrue(model.CanEdit);
        Assert.AreEqual(string.Empty, model.Code);
        Assert.AreEqual(0, client.VerificationCalls);
    }

    private static StubAuthenticationClient CreateClient()
    {
        return new StubAuthenticationClient(_ => Task.FromResult(Pending()));
    }

    private static LoginAttemptResult Pending()
    {
        return new LoginAttemptResult
        {
            Status = LoginAttemptStatus.ChallengeRequired, ChallengeToken = "pending-token",
            MaskedEmail = "p***@example.test"
        };
    }
}
