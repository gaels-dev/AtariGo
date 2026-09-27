using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using AtariGo.Client.Properties;
using AtariGo.Client.Commands;
using AtariGo.Client.Services;
using AtariGo.Client.ViewModels.Dialogs;
using AtariGo.Contracts;
using Grpc.Core;

namespace AtariGo.Client.ViewModels
{
    public class LoginViewModel : ViewModelBase
    {
        private readonly INavigationService _navigationService;
        private readonly IAuthenticationClient _authenticationClient;

        private string _identifier = string.Empty;
        private string _loginMessage = string.Empty;
        private bool _isLanguageOverlayVisible;
        private string _selectedCultureCode = "es";
        private ViewModelBase? _currentDialogViewModel;

        public LoginViewModel(
            INavigationService navigationService,
            IAuthenticationClient? authenticationClient = null)
        {
            ArgumentNullException.ThrowIfNull(navigationService);
            _navigationService = navigationService;
            _authenticationClient = authenticationClient ?? new GrpcAuthenticationClient();

            ChangeLanguageCommand = new RelayCommand(() => IsLanguageOverlayVisible = true);
            CancelLanguageCommand = new RelayCommand(() => IsLanguageOverlayVisible = false);
            ConfirmLanguageCommand = new RelayCommand(ConfirmLanguage);
            SubmitCommand = new AsyncRelayCommand(SubmitAsync);
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
                if (SetProperty(ref _currentDialogViewModel, value))
                {
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

        public void OpenRegisterDialog()
        {
            CurrentDialogViewModel = new RegisterDialogViewModel(_navigationService);
        }

        public void OpenVerificationDialog(string username)
        {
            CurrentDialogViewModel = new VerificationDialogViewModel(
                username,
                _navigationService);
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

        private async Task SubmitAsync(object? parameter)
        {
            string password = parameter as string ?? string.Empty;
            string identifier = Identifier.Trim();

            if (string.IsNullOrWhiteSpace(identifier) || string.IsNullOrWhiteSpace(password))
            {
                LoginMessage = GetLocalizedMessage("Login_Err_Required");
                return;
            }

            LoginMessage = string.Empty;

            try
            {
                LoginResponse response = await _authenticationClient.LoginAsync(identifier, password);

                switch (response.Result)
                {
                    case LoginResult.Success:
                        LoginMessage = GetLocalizedMessage("Login_Msg_Success");
                        await Task.Delay(700);
                        _navigationService.NavigateToMainWindow(
                            isGuest: false,
                            response.UserName,
                            response.SessionToken);
                        break;
                    case LoginResult.UserNotFound:
                        LoginMessage = GetLocalizedMessage("Login_Err_UserNotFound");
                        break;
                    case LoginResult.IncorrectPassword:
                        LoginMessage = GetLocalizedMessage("Login_Err_IncorrectPassword");
                        break;
                    case LoginResult.AccountRestricted:
                        LoginMessage = GetLocalizedMessage("Login_Err_AccountRestricted");
                        break;
                    default:
                        LoginMessage = GetLocalizedMessage("Login_Err_ServiceUnavailable");
                        break;
                }
            }
            catch (RpcException)
            {
                LoginMessage = GetLocalizedMessage("Login_Err_ServiceUnavailable");
            }
            catch (Exception)
            {
                LoginMessage = GetLocalizedMessage("Login_Err_ServiceUnavailable");
            }
        }

        private static string GetLocalizedMessage(string key) =>
            Resources.ResourceManager.GetString(key, CultureInfo.CurrentUICulture)
            ?? key;

        private void GuestLogin()
        {
            _navigationService.NavigateToMainWindow(isGuest: true, "Guest");
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
