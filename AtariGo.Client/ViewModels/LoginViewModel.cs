using System;
using System.Globalization;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Input;
using AtariGo.Client.Commands;
using AtariGo.Client.ViewModels.Dialogs;

namespace AtariGo.Client.ViewModels
{
    public class LoginViewModel : ViewModelBase
    {
        private string _email = string.Empty;
        private bool _isLanguageOverlayVisible;
        private string _selectedCultureCode = "es";
        private ViewModelBase? _currentDialogViewModel;

        private readonly Action<bool, string> _onLoginSuccess;
        private readonly Action _onExit;
        private readonly Action _onRefreshWindow;

        public LoginViewModel(Action<bool, string> onLoginSuccess, Action onExit, Action onRefreshWindow)
        {
            _onLoginSuccess = onLoginSuccess ?? throw new ArgumentNullException(nameof(onLoginSuccess));
            _onExit = onExit ?? throw new ArgumentNullException(nameof(onExit));
            _onRefreshWindow = onRefreshWindow ?? throw new ArgumentNullException(nameof(onRefreshWindow));

            ChangeLanguageCommand = new RelayCommand(() => IsLanguageOverlayVisible = true);
            CancelLanguageCommand = new RelayCommand(() => IsLanguageOverlayVisible = false);
            ConfirmLanguageCommand = new RelayCommand(ConfirmLanguage);
            SubmitCommand = new RelayCommand(Submit);
            GuestCommand = new RelayCommand(GuestLogin);
            ExitCommand = new RelayCommand(_onExit);
            OpenRegisterCommand = new RelayCommand(OpenRegisterDialog);
            OpenForgotPasswordCommand = new RelayCommand(OpenForgotPasswordDialog);
        }

        public string Email
        {
            get => _email;
            set => SetProperty(ref _email, value);
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

        public bool IsDialogVisible => _currentDialogViewModel != null;

        public ICommand ChangeLanguageCommand { get; }

        public ICommand CancelLanguageCommand { get; }

        public ICommand ConfirmLanguageCommand { get; }

        public ICommand SubmitCommand { get; }

        public ICommand GuestCommand { get; }

        public ICommand ExitCommand { get; }

        public ICommand OpenRegisterCommand { get; }

        public ICommand OpenForgotPasswordCommand { get; }

        private void Submit(object? parameter)
        {
            string password = string.Empty;
            if (parameter is PasswordBox passwordBox)
            {
                password = passwordBox.Password;
            }

            string username = string.IsNullOrWhiteSpace(_email) ? "John Go" : _email;
            _onLoginSuccess(false, username);
        }

        private void GuestLogin()
        {
            _onLoginSuccess(true, "Guest");
        }

        private void ConfirmLanguage()
        {
            if (!string.IsNullOrEmpty(_selectedCultureCode))
            {
                var culture = new CultureInfo(_selectedCultureCode);
                Thread.CurrentThread.CurrentCulture = culture;
                Thread.CurrentThread.CurrentUICulture = culture;
                CultureInfo.DefaultThreadCurrentCulture = culture;
                CultureInfo.DefaultThreadCurrentUICulture = culture;

                IsLanguageOverlayVisible = false;
                _onRefreshWindow();
            }
        }

        private void OpenRegisterDialog()
        {
            var registerVM = new RegisterDialogViewModel(
                onCloseRequested: () => CurrentDialogViewModel = null,
                onProceedToVerification: (username, email) =>
                {
                    OpenVerificationDialog(username);
                });

            CurrentDialogViewModel = registerVM;
        }

        private void OpenVerificationDialog(string username)
        {
            var verificationVM = new VerificationDialogViewModel(
                username: username,
                onBackRequested: OpenRegisterDialog,
                onVerificationSuccess: finalUser =>
                {
                    CurrentDialogViewModel = null;
                    _onLoginSuccess(false, finalUser);
                });

            CurrentDialogViewModel = verificationVM;
        }

        private void OpenForgotPasswordDialog()
        {
            var forgotVM = new ForgotPasswordDialogViewModel(
                onCloseRequested: () => CurrentDialogViewModel = null,
                onProceedToVerification: emailOrUser =>
                {
                    OpenForgotVerificationDialog();
                });

            CurrentDialogViewModel = forgotVM;
        }

        private void OpenForgotVerificationDialog()
        {
            var verificationVM = new ForgotVerificationDialogViewModel(
                onBackRequested: OpenForgotPasswordDialog,
                onProceedToNewPassword: OpenNewPasswordDialog);

            CurrentDialogViewModel = verificationVM;
        }

        private void OpenNewPasswordDialog()
        {
            var newPasswordVM = new NewPasswordDialogViewModel(
                onCancelRequested: () => CurrentDialogViewModel = null,
                onPasswordChangedSuccess: () =>
                {
                    CurrentDialogViewModel = null;
                });

            CurrentDialogViewModel = newPasswordVM;
        }
    }
}