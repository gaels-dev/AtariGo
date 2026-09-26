using System;
using System.Windows.Input;
using AtariGo.Client.Commands;
using AtariGo.Client.Services;

namespace AtariGo.Client.ViewModels.Dialogs
{
    public class ForgotVerificationDialogViewModel : ViewModelBase
    {
        private string _verificationCode = string.Empty;
        private string _errorMessage = string.Empty;

        private readonly INavigationService _navigationService;

        public ForgotVerificationDialogViewModel(INavigationService navigationService)
        {
            ArgumentNullException.ThrowIfNull(navigationService);
            _navigationService = navigationService;

            ConfirmCommand = new RelayCommand(VerifyCode);
            CancelCommand = new RelayCommand(_navigationService.OpenForgotPasswordDialog);
        }

        public string VerificationCode
        {
            get => _verificationCode;
            set => SetProperty(ref _verificationCode, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public ICommand ConfirmCommand { get; }

        public ICommand CancelCommand { get; }

        private void VerifyCode()
        {
            if (string.IsNullOrWhiteSpace(_verificationCode))
            {
                ErrorMessage = Properties.Resources.ForgotVerification_Err_EmptyCode;
                return;
            }

            ErrorMessage = string.Empty;
            _navigationService.OpenNewPasswordDialog();
        }
    }
}
