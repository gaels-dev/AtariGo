using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Grpc.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using AtariGo.Client.Models;
using AtariGo.Client.Properties;
using AtariGo.Client.Services;
using AtariGo.Client.Tests.Services;
using AtariGo.Client.ViewModels.Dialogs;
using AtariGo.Contracts;

namespace AtariGo.Client.Tests.ViewModels
{
    [TestClass]
    public sealed class VerificationDialogViewModelTests
    {
        private const int ExpectedRegistrationCalls = 1;

        [TestMethod]
        [DataRow(StatusCode.DeadlineExceeded)]
        [DataRow(StatusCode.Unavailable)]
        public async Task ConfirmCommand_LostResponse_DoesNotReplayRegistration(
            StatusCode statusCode)
        {
            // Arrange
            INavigationService navigation = DispatchProxy.Create<INavigationService,
                RecordingNavigationProxy>();
            int calls = 0;
            var registration = new StubRegistrationClient(_ =>
            {
                calls++;
                return Task.FromException<RegisterAccountResponse>(
                    new RpcException(new Status(statusCode, "Response lost")));
            });
            using var viewModel = new VerificationDialogViewModel(
                new RegistrationDraft(), navigation, registration);

            // Act
            await CommandExecution.ExecuteAsync(viewModel.ConfirmCommand, "confirmation text");

            // Assert
            Assert.AreEqual(ExpectedRegistrationCalls, calls);
            Assert.AreEqual(Resources.ResourceManager.GetString(
                "Registration_Err_Unconfirmed", CultureInfo.CurrentUICulture),
                viewModel.ErrorMessage);
            Assert.IsFalse(((RecordingNavigationProxy)navigation).Calls
                .Contains(nameof(INavigationService.ShowRegistrationResult)));
        }

        [TestMethod]
        public async Task CancelCommand_LateSuccessfulResponse_DoesNotShowRegistrationResult()
        {
            // Arrange
            INavigationService navigation = DispatchProxy.Create<INavigationService,
                RecordingNavigationProxy>();
            var pendingResponse = new TaskCompletionSource<RegisterAccountResponse>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            CancellationToken requestToken = default;
            var registration = new StubRegistrationClient(token =>
            {
                requestToken = token;
                return pendingResponse.Task;
            });
            using var viewModel = new VerificationDialogViewModel(
                new RegistrationDraft(), navigation, registration);
            Task command = CommandExecution.ExecuteAsync(viewModel.ConfirmCommand, "text");

            // Act
            viewModel.CancelCommand.Execute(null);
            pendingResponse.SetResult(new RegisterAccountResponse
            {
                Result = RegistrationResult.Success
            });
            await command;

            // Assert
            Assert.IsTrue(requestToken.IsCancellationRequested);
            Assert.IsFalse(((RecordingNavigationProxy)navigation).Calls
                .Contains(nameof(INavigationService.ShowRegistrationResult)));
        }
    }
}
