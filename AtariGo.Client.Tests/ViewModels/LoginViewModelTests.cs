using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Grpc.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using AtariGo.Client.Properties;
using AtariGo.Client.Services;
using AtariGo.Client.Tests.Services;
using AtariGo.Client.ViewModels;
using AtariGo.Contracts;

namespace AtariGo.Client.Tests.ViewModels
{
    [TestClass]
    public sealed class LoginViewModelTests
    {
        [TestMethod]
        [DataRow(StatusCode.DeadlineExceeded, "Connection_Err_Timeout")]
        [DataRow(StatusCode.Unavailable, "Connection_Err_Unavailable")]
        public async Task SubmitCommand_TransportFailure_ShowsSpecificMessage(
            StatusCode statusCode,
            string expectedResourceKey)
        {
            // Arrange
            INavigationService navigation = DispatchProxy.Create<INavigationService,
                RecordingNavigationProxy>();
            var authentication = new StubAuthenticationClient(_ =>
                Task.FromException<LoginResponse>(
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
            var pendingResponse = new TaskCompletionSource<LoginResponse>(
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
            pendingResponse.SetResult(new LoginResponse { Result = LoginResult.Success });
            await command;

            // Assert
            Assert.IsTrue(requestToken.IsCancellationRequested);
            Assert.IsFalse(((RecordingNavigationProxy)navigation).Calls
                .Contains(nameof(INavigationService.NavigateToMainWindow)));
        }
    }
}
