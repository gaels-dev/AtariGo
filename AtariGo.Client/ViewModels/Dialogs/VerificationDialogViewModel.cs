using System.Diagnostics;
using System.Globalization;
using System.Windows.Input;

using Grpc.Core;

using AtariGo.Client.Commands;
using AtariGo.Client.Models;
using AtariGo.Client.Properties;
using AtariGo.Client.Services;
using AtariGo.Contracts;

namespace AtariGo.Client.ViewModels.Dialogs
{
    public sealed class VerificationDialogViewModel : ViewModelBase, IDisposable
    {
        private readonly RegistrationDraft _registrationDraft;
        private readonly INavigationService _navigationService;
        private readonly IRegistrationClient _registrationClient;
        private readonly CancellationTokenSource _lifetimeCancellation =
            new CancellationTokenSource();
        private bool _isDisposed;
        private string _verificationCode = string.Empty;
        private string _errorMessage = string.Empty;

        public VerificationDialogViewModel(
            RegistrationDraft registrationDraft,
            INavigationService navigationService,
            IRegistrationClient registrationClient)
        {
            ArgumentNullException.ThrowIfNull(registrationDraft);
            ArgumentNullException.ThrowIfNull(navigationService);
            ArgumentNullException.ThrowIfNull(registrationClient);

            _registrationDraft = registrationDraft;
            _navigationService = navigationService;
            _registrationClient = registrationClient;
            ConfirmCommand = new AsyncRelayCommand(
                ConfirmRegistrationAsync,
                HandleConfirmationFailure);
            CancelCommand = new RelayCommand(CancelRegistration);
        }

        public string VerificationCode
        {
            get => _verificationCode;
            set => SetProperty(ref _verificationCode, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            private set => SetProperty(ref _errorMessage, value);
        }

        public ICommand ConfirmCommand { get; }

        public ICommand CancelCommand { get; }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _lifetimeCancellation.Cancel();
            _lifetimeCancellation.Dispose();
        }

        private void CancelRegistration()
        {
            Dispose();
            _navigationService.ReturnToRegisterDialog();
        }

        private async Task ConfirmRegistrationAsync(object? parameter)
        {
            if (_isDisposed)
            {
                return;
            }

            string confirmationText = parameter as string ?? string.Empty;
            if (string.IsNullOrWhiteSpace(confirmationText))
            {
                ErrorMessage = GetLocalizedMessage("Verification_Err_EmptyCode");
                return;
            }

            ErrorMessage = string.Empty;
            CancellationToken cancellationToken = _lifetimeCancellation.Token;
            try
            {
                RegisterAccountResponse response =
                    await _registrationClient.RegisterAccountAsync(
                        _registrationDraft,
                        confirmationText,
                        cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                _navigationService.ShowRegistrationResult(response.Result);
            }
            catch (Exception exception)
                when (GrpcCallPolicy.IsRequestedCancellation(exception, cancellationToken))
            {
                Trace.TraceInformation(
                    "Registration wait cancelled because its dialog was closed.");
            }
            catch (RpcException exception)
            {
                ErrorMessage = GrpcFailureMessages.GetRegistrationMessage(exception);
            }
        }

        private void HandleConfirmationFailure(Exception exception)
        {
            if (!_isDisposed)
            {
                ErrorMessage = GrpcFailureMessages.GetMessage(exception);
            }
        }

        private static string GetLocalizedMessage(string resourceKey)
        {
            return Resources.ResourceManager.GetString(
                resourceKey,
                CultureInfo.CurrentUICulture) ?? resourceKey;
        }
    }
}
