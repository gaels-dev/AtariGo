using System;
using System.Windows.Input;
using AtariGo.Client.Commands;

namespace AtariGo.Client.ViewModels.Dialogs
{
    public class ForgotVerificationDialogViewModel : ViewModelBase
    {
        private string _verificationCode = string.Empty;
        private string _errorMessage = string.Empty;

        private readonly Action _onBackRequested;
        private readonly Action _onProceedToNewPassword;

        public ForgotVerificationDialogViewModel(Action onBackRequested, Action onProceedToNewPassword)
        {
            _onBackRequested = onBackRequested ?? throw new ArgumentNullException(nameof(onBackRequested));
            _onProceedToNewPassword = onProceedToNewPassword ?? throw new ArgumentNullException(nameof(onProceedToNewPassword));

            ConfirmCommand = new RelayCommand(VerifyCode);
            CancelCommand = new RelayCommand(_onBackRequested);
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
            _onProceedToNewPassword();
        }
    }
}