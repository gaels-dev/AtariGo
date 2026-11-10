using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

using Grpc.Core;

using AtariGo.Client.Commands;
using AtariGo.Client.Models;
using AtariGo.Client.Properties;
using AtariGo.Client.Services;
using AtariGo.Client.ViewModels.Dialogs;
using AtariGo.Contracts;

namespace AtariGo.Client.ViewModels
{
    public class LoginViewModel : ViewModelBase, IDisposable
    {
        private static readonly TimeSpan s_loginSuccessMessageDelay =
            TimeSpan.FromSeconds(1);

        private readonly INavigationService _navigationService;
        private readonly IAuthenticationClient _authenticationClient;
        private readonly IRegistrationClient _registrationClient;
        private readonly CancellationTokenSource _lifetimeCancellation =
            new CancellationTokenSource();
        private bool _isDisposed;
        private RegisterDialogViewModel? _registerDialogViewModel;
        private GuestRegistrationDialogViewModel?
            _guestRegistrationDialogViewModel;

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

        public ICommand ChangeLanguageCommand { get; }

        public ICommand CancelLanguageCommand { get; }

        public ICommand ConfirmLanguageCommand { get; }

        public ICommand SubmitCommand { get; }

        public ICommand GuestCommand { get; }

        public ICommand ExitCommand { get; }

        public ICommand OpenRegisterCommand { get; }

        public ICommand OpenForgotPasswordCommand { get; }

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
            LoginMessage = GetLocalizedMessage("Connection_Err_SessionExpired");
        }

        public void OpenRegisterDialog()
        {
            _registerDialogViewModel = new RegisterDialogViewModel(_navigationService);
            CurrentDialogViewModel = _registerDialogViewModel;
        }

        public void OpenVerificationDialog(RegistrationDraft registrationDraft)
        {
            CurrentDialogViewModel = new VerificationDialogViewModel(
                registrationDraft,
                _navigationService,
                _registrationClient);
        }

        public void ReturnToRegisterDialog()
        {
            CurrentDialogViewModel = _registerDialogViewModel;
        }

        public void DiscardRegistration()
        {
            _registerDialogViewModel?.ClearSensitiveData();
            _registerDialogViewModel = null;
            CurrentDialogViewModel = null;
        }

        public void ShowRegistrationResult(RegistrationResult result)
        {
            bool succeeded = result == RegistrationResult.Success;
            ViewModelBase? previousDialog = CurrentDialogViewModel;
            string resourceKey = result switch
            {
                RegistrationResult.Success => "RegistrationResult_Msg_Success",
                RegistrationResult.UsernameTaken =>
                    "RegistrationResult_Msg_UsernameTaken",
                RegistrationResult.EmailTaken => "RegistrationResult_Msg_EmailTaken",
                _ => "RegistrationResult_Msg_Error"
            };

            CurrentDialogViewModel = new RegistrationResultDialogViewModel(
                GetLocalizedMessage(resourceKey),
                GetLocalizedMessage("RegistrationResult_Btn_Close"),
                () => CloseRegistrationResult(succeeded, previousDialog));
        }

        public void OpenForgotPasswordDialog()
        {
            CurrentDialogViewModel = new ForgotPasswordDialogViewModel(_navigationService);
        }

        public void OpenForgotVerificationDialog()
        {
            CurrentDialogViewModel = new ForgotVerificationDialogViewModel(_navigationService);
        }

        public void OpenNewPasswordDialog()
        {
            CurrentDialogViewModel = new NewPasswordDialogViewModel(_navigationService);
        }

        public void CloseDialog()
        {
            CurrentDialogViewModel = null;
        }

        private void CloseRegistrationResult(bool succeeded, ViewModelBase? previousDialog)
        {
            if (succeeded)
            {
                _registerDialogViewModel?.ClearSensitiveData();
                _registerDialogViewModel = null;
            }

            CurrentDialogViewModel = succeeded
                ? null
                : _registerDialogViewModel ?? previousDialog;
        }

        private async Task SubmitAsync(object? parameter)
        {
            if (_isDisposed)
            {
                return;
            }

            string password = parameter as string ?? string.Empty;
            string identifier = Identifier.Trim();

            if (string.IsNullOrWhiteSpace(identifier) || string.IsNullOrWhiteSpace(password))
            {
                LoginMessage = GetLocalizedMessage("Login_Err_Required");
                return;
            }

            LoginMessage = string.Empty;
            CancellationToken cancellationToken = _lifetimeCancellation.Token;

            try
            {
                LoginResponse response = await _authenticationClient.LoginAsync(
                    identifier,
                    password,
                    cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                await ApplyLoginResponseAsync(response, cancellationToken);
            }
            catch (Exception exception)
                when (GrpcCallPolicy.IsRequestedCancellation(exception, cancellationToken))
            {
                Trace.TraceInformation("Login cancelled because its window was closed.");
            }
            catch (RpcException exception)
            {
                LoginMessage = GrpcFailureMessages.GetMessage(exception);
            }
        }

        private async Task ApplyLoginResponseAsync(
            LoginResponse response,
            CancellationToken cancellationToken)
        {
            string resourceKey = response.Result switch
            {
                LoginResult.Success => "Login_Msg_Success",
                LoginResult.UserNotFound => "Login_Err_UserNotFound",
                LoginResult.IncorrectPassword => "Login_Err_IncorrectPassword",
                LoginResult.AccountRestricted => "Login_Err_AccountRestricted",
                _ => "Connection_Err_RequestFailed"
            };
            LoginMessage = GetLocalizedMessage(resourceKey);

            if (response.Result == LoginResult.Success)
            {
                await Task.Delay(s_loginSuccessMessageDelay, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
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
                LoginMessage = GrpcFailureMessages.GetMessage(exception);
            }
        }

        private static string GetLocalizedMessage(string key) =>
            Resources.ResourceManager.GetString(key, CultureInfo.CurrentUICulture)
            ?? key;

        private void GuestLogin()
        {
            _guestRegistrationDialogViewModel =
                new GuestRegistrationDialogViewModel(
                    _navigationService);
            CurrentDialogViewModel =
                _guestRegistrationDialogViewModel;
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
    }
}
