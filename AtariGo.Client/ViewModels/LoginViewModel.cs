using System.Diagnostics;
using System.Globalization;
using System.Windows.Input;

using AtariGo.Client.Commands;
using AtariGo.Client.Models;
using AtariGo.Client.Services;
using AtariGo.Client.ViewModels.Dialogs;
using AtariGo.Contracts.Validation;

namespace AtariGo.Client.ViewModels
{
    public class LoginViewModel : ViewModelBase, IDisposable
    {
        private readonly INavigationService _navigationService;
        private readonly IAuthenticationClient _authenticationClient;
        private readonly IRegistrationClient _registrationClient;
        private readonly CancellationTokenSource _lifetimeCancellation =
            new CancellationTokenSource();
        private bool _isDisposed;
        private bool _isBusy;
        private string _identifier = string.Empty;
        private string _loginMessage = string.Empty;
        private bool _isLanguageOverlayVisible;
        private string _selectedCultureCode = "es";
        private ViewModelBase? _currentDialogViewModel;

        public LoginViewModel(
            INavigationService navigationService,
            IAuthenticationClient? authenticationClient = null,
            IRegistrationClient? registrationClient = null)
        {
            ArgumentNullException.ThrowIfNull(navigationService);
            _navigationService = navigationService;
            _authenticationClient = authenticationClient ?? new GrpcAuthenticationClient();
            _registrationClient = registrationClient ?? new GrpcRegistrationClient();

            ChangeLanguageCommand = new RelayCommand(() => IsLanguageOverlayVisible = true);
            CancelLanguageCommand = new RelayCommand(() => IsLanguageOverlayVisible = false);
            ConfirmLanguageCommand = new RelayCommand(ConfirmLanguage);
            SubmitCommand = new AsyncRelayCommand(
                SubmitAsync,
                HandleSubmitFailure);
            GuestCommand = new RelayCommand(GuestLogin);
            ExitCommand = new RelayCommand(_navigationService.ExitApplication);
            OpenRegisterCommand = new RelayCommand(_navigationService.OpenRegisterDialog);
            OpenForgotPasswordCommand = new RelayCommand(
                _navigationService.OpenForgotPasswordDialog);
        }

        public string Identifier
        {
            get => _identifier;
            set => SetProperty(ref _identifier, value);
        }

        public string LoginMessage
        {
            get => _loginMessage;
            private set => SetProperty(ref _loginMessage, value);
        }

        public bool IsLanguageOverlayVisible
        {
            get => _isLanguageOverlayVisible;
            set => SetProperty(ref _isLanguageOverlayVisible, value);
        }

        public string SelectedCultureCode
        {
            get => _selectedCultureCode;
            set => SetProperty(ref _selectedCultureCode, value);
        }

        public ViewModelBase? CurrentDialogViewModel
        {
            get => _currentDialogViewModel;
            private set
            {
                ViewModelBase? previousDialog = _currentDialogViewModel;
                if (SetProperty(ref _currentDialogViewModel, value))
                {
                    (previousDialog as IDisposable)?.Dispose();
                    OnPropertyChanged(nameof(IsDialogVisible));
                }
            }
        }

        public bool IsDialogVisible => _currentDialogViewModel is not null;

        public bool CanEditCredentials => !_isDisposed && !_isBusy;

        public ICommand ChangeLanguageCommand
        {
            get;
        }

        public ICommand CancelLanguageCommand
        {
            get;
        }

        public ICommand ConfirmLanguageCommand
        {
            get;
        }

        public ICommand SubmitCommand
        {
            get;
        }

        public ICommand GuestCommand
        {
            get;
        }

        public ICommand ExitCommand
        {
            get;
        }

        public ICommand OpenRegisterCommand
        {
            get;
        }

        public ICommand OpenForgotPasswordCommand
        {
            get;
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _lifetimeCancellation.Cancel();
            (CurrentDialogViewModel as IDisposable)?.Dispose();
            _lifetimeCancellation.Dispose();
        }

        public void ShowSessionExpiredMessage()
        {
            LoginMessage = AuthenticationMessages.GetText("Connection_Err_SessionExpired");
        }

        public void OpenRegisterDialog()
        {
            if (!CanEditCredentials)
            {
                return;
            }
            CurrentDialogViewModel = new RegisterDialogViewModel(
                _navigationService, _registrationClient);
        }

        public void ShowRegistrationSuccess()
        {
            if (CurrentDialogViewModel is RegisterDialogViewModel registration)
            {
                Identifier = registration.UserName.Trim();
            }
            CurrentDialogViewModel = new RegistrationResultDialogViewModel(
                AuthenticationMessages.GetText("RegistrationResult_Msg_Success"),
                AuthenticationMessages.GetText("RegistrationResult_Btn_Close"), CloseDialog);
        }

        public void OpenForgotPasswordDialog()
        {
            if (!CanEditCredentials)
            {
                return;
            }
            CurrentDialogViewModel = new PasswordRecoveryDialogViewModel(
                new GrpcPasswordRecoveryClient(), _navigationService);
        }

        public void CloseDialog()
        {
            CurrentDialogViewModel = null;
        }

        private async Task SubmitAsync(object? parameter)
        {
            if (!CanEditCredentials || IsDialogVisible)
            {
                return;
            }

            string password = parameter as string ?? string.Empty;
            string identifier = Identifier.Trim();

            if (string.IsNullOrWhiteSpace(identifier) || string.IsNullOrEmpty(password))
            {
                LoginMessage = AuthenticationMessages.GetText("Login_Err_Required");
                return;
            }

            if (identifier.Length > EmailAddressRules.MaximumLength
                || password.Length > PasswordPolicy.MaximumPasswordLength)
            {
                LoginMessage = AuthenticationMessages.GetText(
                    "LoginVerification_Result_InvalidCredentials");
                return;
            }
            LoginMessage = string.Empty;
            CancellationToken cancellationToken = _lifetimeCancellation.Token;
            SetLoginBusy(true);

            try
            {
                LoginAttemptResult response = await _authenticationClient.LoginAsync(
                    identifier,
                    password,
                    cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                ApplyLoginResponse(response);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Trace.TraceInformation("Login cancelled because its window was closed.");
            }
            finally
            {
                password = string.Empty;
                SetLoginBusy(false);
            }
        }

        private void ApplyLoginResponse(LoginAttemptResult response)
        {
            LoginMessage = AuthenticationMessages.GetMessage(response);
            if (response.Status is LoginAttemptStatus.ChallengeRequired
                or LoginAttemptStatus.DeliveryFailed
                && !string.IsNullOrWhiteSpace(response.ChallengeToken))
            {
                CurrentDialogViewModel = new LoginVerificationDialogViewModel(
                    _authenticationClient, response, CompleteLogin);
            }
            else if (response.Status == LoginAttemptStatus.Success)
            {
                LoginMessage = AuthenticationMessages.GetText(
                    "LoginVerification_Result_Unavailable");
            }
        }

        private void CompleteLogin(LoginAttemptResult response)
        {
            if (_isDisposed)
            {
                return;
            }
            CurrentDialogViewModel = null;
            if (response.Status == LoginAttemptStatus.Success
                && !string.IsNullOrWhiteSpace(response.SessionToken))
            {
                _navigationService.NavigateToMainWindow(new ClientSessionContext
                {
                    IsGuest = false,
                    PlayerName = response.UserName,
                    SessionToken = response.SessionToken
                });
            }
        }

        private void HandleSubmitFailure(Exception exception)
        {
            if (!_isDisposed)
            {
                LoginMessage = AuthenticationMessages.GetText(
                    "LoginVerification_Result_Unconfirmed");
            }
        }

        private void GuestLogin()
        {
            if (!CanEditCredentials)
            {
                return;
            }
            CurrentDialogViewModel = new GuestRegistrationDialogViewModel(
                _navigationService);
        }

        private void ConfirmLanguage()
        {
            if (string.IsNullOrEmpty(_selectedCultureCode))
            {
                return;
            }

            var culture = new CultureInfo(_selectedCultureCode);
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;

            IsLanguageOverlayVisible = false;
            _navigationService.RefreshLoginView();
        }

        private void SetLoginBusy(bool isBusy)
        {
            if (!_isDisposed)
            {
                _isBusy = isBusy;
                OnPropertyChanged(nameof(CanEditCredentials));
            }
        }
    }
}
