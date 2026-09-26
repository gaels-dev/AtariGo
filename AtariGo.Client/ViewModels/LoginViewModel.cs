using System;
using System.Globalization;
using System.Threading;
using System.Windows.Input;
using AtariGo.Client.Commands;
using AtariGo.Client.Services;
using AtariGo.Client.ViewModels.Dialogs;

namespace AtariGo.Client.ViewModels
{
    public class LoginViewModel : ViewModelBase
    {
        private readonly INavigationService _navigationService;

        private string _email = string.Empty;
        private bool _isLanguageOverlayVisible;
        private string _selectedCultureCode = "es";
        private ViewModelBase? _currentDialogViewModel;

        public LoginViewModel(INavigationService navigationService)
        {
            ArgumentNullException.ThrowIfNull(navigationService);
            _navigationService = navigationService;

            ChangeLanguageCommand = new RelayCommand(() => IsLanguageOverlayVisible = true);
            CancelLanguageCommand = new RelayCommand(() => IsLanguageOverlayVisible = false);
            ConfirmLanguageCommand = new RelayCommand(ConfirmLanguage);
            SubmitCommand = new RelayCommand(Submit);
            GuestCommand = new RelayCommand(GuestLogin);
            ExitCommand = new RelayCommand(_navigationService.ExitApplication);
            OpenRegisterCommand = new RelayCommand(_navigationService.OpenRegisterDialog);
            OpenForgotPasswordCommand = new RelayCommand(
                _navigationService.OpenForgotPasswordDialog);
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

        private void Submit()
        {
            string playerName = string.IsNullOrWhiteSpace(_email) ? "John Go" : _email;
            _navigationService.NavigateToMainWindow(isGuest: false, playerName);
        }

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
