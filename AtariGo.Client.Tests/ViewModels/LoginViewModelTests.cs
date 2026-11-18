using System.Globalization;
using System.Reflection;


using Grpc.Core;

using AtariGo.Client.Models;
using AtariGo.Client.Properties;
using AtariGo.Client.Services;
using AtariGo.Client.Tests.Services;
using AtariGo.Client.ViewModels;
using AtariGo.Client.ViewModels.Dialogs;
using AtariGo.Contracts;

namespace AtariGo.Client.Tests.ViewModels
{
    [TestClass]
    [TestCategory("Unit")]
    public sealed class LoginViewModelTests
    {
        [TestMethod]
        public async Task SubmitCommand_DirectSuccessWithoutCode_DoesNotNavigate()
        {
            // Arrange
            INavigationService navigation = DispatchProxy.Create<INavigationService,
                RecordingNavigationProxy>();
            var authentication = new StubAuthenticationClient(_ => Task.FromResult(
                new LoginAttemptResult
                {
                    Status = LoginAttemptStatus.Success, SessionToken = "bypass"
                }));
            var registration = new StubRegistrationClient(_ =>
                Task.FromResult(new RegisterAccountResponse()));
            using var model = new LoginViewModel(navigation, authentication, registration)
            {
                Identifier = "Player"
            };
            // Act
            await CommandExecution.ExecuteAsync(model.SubmitCommand, "Secret8");
            // Assert
            Assert.IsNull(((RecordingNavigationProxy)navigation).SessionContext);
            Assert.IsNull(model.CurrentDialogViewModel);
        }

        [TestMethod]
        [DataRow(StatusCode.DeadlineExceeded, "LoginVerification_Result_Unconfirmed")]
        [DataRow(StatusCode.Unavailable, "LoginVerification_Result_Unconfirmed")]
        public async Task SubmitCommand_TransportFailure_ShowsUnconfirmedMessage(
            StatusCode statusCode,
            string expectedResourceKey)
        {
            // Arrange
            INavigationService navigation = DispatchProxy.Create<INavigationService,
                RecordingNavigationProxy>();
            var authentication = new StubAuthenticationClient(_ =>
                Task.FromException<LoginAttemptResult>(
                    new RpcException(new Status(statusCode, "Test"))));
            var registration = new StubRegistrationClient(_ =>
                Task.FromResult(new RegisterAccountResponse()));
            using var viewModel = new LoginViewModel(navigation, authentication, registration);
            viewModel.Identifier = "player";

            // Act
            await CommandExecution.ExecuteAsync(viewModel.SubmitCommand, "password");

            // Assert
            Assert.AreEqual(Resources.ResourceManager.GetString(
                expectedResourceKey, CultureInfo.CurrentUICulture), viewModel.LoginMessage);
        }

        [TestMethod]
        public async Task Dispose_PendingLoginSuccess_DoesNotNavigateToMainWindow()
        {
            // Arrange
            INavigationService navigation = DispatchProxy.Create<INavigationService,
                RecordingNavigationProxy>();
            var pendingResponse = new TaskCompletionSource<LoginAttemptResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            CancellationToken requestToken = default;
            var authentication = new StubAuthenticationClient(token =>
            {
                requestToken = token;
                return pendingResponse.Task;
            });
            var registration = new StubRegistrationClient(_ =>
                Task.FromResult(new RegisterAccountResponse()));
            using var viewModel = new LoginViewModel(navigation, authentication, registration);
            viewModel.Identifier = "player";
            Task command = CommandExecution.ExecuteAsync(viewModel.SubmitCommand, "password");

            // Act
            viewModel.Dispose();
            pendingResponse.SetResult(new LoginAttemptResult
            {
                Status = LoginAttemptStatus.Success
            });
            await command;

            // Assert
            Assert.IsTrue(requestToken.IsCancellationRequested);
            Assert.IsFalse(((RecordingNavigationProxy)navigation).Calls
                .Contains(nameof(INavigationService.NavigateToMainWindow)));
        }
        [TestMethod]
        public async Task SubmitCommand_ConfirmedCode_PreservesSessionContextDuringNavigation()
        {
            // Arrange
            INavigationService navigation = DispatchProxy.Create<INavigationService,
                RecordingNavigationProxy>();
            var authentication = new StubAuthenticationClient(_ => Task.FromResult(
                new LoginAttemptResult
            {
                Status = LoginAttemptStatus.ChallengeRequired, ChallengeToken = "pending"
            }));
            authentication.Verify = _ => Task.FromResult(new LoginAttemptResult
            {
                Status = LoginAttemptStatus.Success, UserName = "Player",
                SessionToken = "test-session"
            });
            var registration = new StubRegistrationClient(_ =>
                Task.FromResult(new RegisterAccountResponse()));
            using var model = new LoginViewModel(navigation, authentication, registration)
            {
                Identifier = "Player"
            };

            // Act
            await CommandExecution.ExecuteAsync(model.SubmitCommand, "password");
            Assert.IsNull(((RecordingNavigationProxy)navigation).SessionContext);
            Assert.IsInstanceOfType<LoginVerificationDialogViewModel>(model.CurrentDialogViewModel);
            var verification = (LoginVerificationDialogViewModel)model.CurrentDialogViewModel;
            verification.Code = "01234567";
            await CommandExecution.ExecuteAsync(verification.ConfirmCommand, null);

            // Assert
            var recorder = (RecordingNavigationProxy)navigation;
            Assert.IsNotNull(recorder.SessionContext);
            Assert.AreEqual("Player", recorder.SessionContext.PlayerName);
            Assert.AreEqual("test-session", recorder.SessionContext.SessionToken);
            Assert.IsFalse(recorder.SessionContext.IsGuest);
        }
    }
}
